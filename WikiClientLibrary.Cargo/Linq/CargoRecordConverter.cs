using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Cargo.Schema.DataAnnotations;

namespace WikiClientLibrary.Cargo.Linq;

public interface ICargoRecordConverter
{

    /// <summary>
    /// Converts JSON record object from MediaWiki Cargo API response into CLR model type.
    /// </summary>
    object DeserializeRecord(IReadOnlyDictionary<MemberInfo, JToken> record, Type modelType);

}

public class CargoRecordConverter : ICargoRecordConverter
{

    private static readonly MethodInfo dictIndexer
        = typeof(IReadOnlyDictionary<string, JToken>).GetProperty("Item")!.GetMethod!;

    private static readonly MethodInfo dictTryGetValue
        = typeof(IReadOnlyDictionary<string, JToken>).GetMethod(nameof(IReadOnlyDictionary<string, JToken>.TryGetValue))!;

    private static readonly MethodInfo deserializeCollectionMethod = typeof(CargoRecordConverter)
        .GetMethod(nameof(DeserializeCollection), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo deserializeStringCollectionMethod = typeof(CargoRecordConverter)
        .GetMethod(nameof(DeserializeStringCollection), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly ConcurrentDictionary<Type, Func<IReadOnlyDictionary<string, JToken>, object>> cachedDeserializers =
        new ConcurrentDictionary<Type, Func<IReadOnlyDictionary<string, JToken>, object>>();

    [return: NotNullIfNotNull("value")]
    private static IList<T>? DeserializeCollection<T>(JToken? value, string separator)
    {
        var values = (string)value;
        if (values == null) return default;
        if (values.Length == 0) return Array.Empty<T>();
        return values.Split(new[] { separator }, StringSplitOptions.None)
            .Select(i => (T)Convert.ChangeType(i, typeof(T), CultureInfo.InvariantCulture))
            .ToList()!;
    }

    [return:NotNullIfNotNull("value")]
    private static IList<string>? DeserializeStringCollection(JToken? value, string separator)
    {
        var values = (string?)value;
        if (values == null) return default;
        if (values.Length == 0) return Array.Empty<string>();
        var result = values.Split(new[] { separator }, StringSplitOptions.None);
        for (int i = 0; i < result.Length; i++)
            result[i] = result[i].Trim();
        return result;
    }

    private static void EmitDeserializeValue(ILGenerator gen, Type targetType, CargoListAttribute? listAttr)
    {
        var elementType = CargoModelUtility.GetCollectionElementType(targetType);
        // Collection property
        if (elementType != null)
        {
            var delimiter = listAttr?.Delimiter ?? CargoListAttribute.DefaultDelimiter;
            // Currently we require cargo list model property declared as ICollection<T> actually.
            if (targetType.IsAssignableFrom(typeof(IList<>).MakeGenericType(elementType).GetTypeInfo()))
            {
                gen.Emit(OpCodes.Ldstr, delimiter);
                if (elementType == typeof(string))
                    gen.EmitCall(OpCodes.Call, deserializeStringCollectionMethod, null);
                else
                    gen.EmitCall(OpCodes.Call, deserializeCollectionMethod.MakeGenericMethod(elementType), null);
            }
            else
            {
                throw new NotSupportedException($"Deserializing cargo list into {targetType} is not supported.");
            }
            return;
        }

        // Not emit nullable value deserialization here since it's too tricky to do it right.
        gen.EmitCall(OpCodes.Call, ValueConverters.GetValueDeserializer(targetType), null);
    }

    public virtual object DeserializeRecord(IReadOnlyDictionary<MemberInfo, JToken> record, Type modelType)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (modelType == null) throw new ArgumentNullException(nameof(modelType));
        if (!cachedDeserializers.TryGetValue(modelType, out var des))
        {
            var builder = new DynamicMethod(
                typeof(CargoRecordConverter) + "$DeserializeRecordImpl[" + modelType + "]",
                modelType,
                new[] { typeof(IReadOnlyDictionary<string, JToken>) },
                typeof(CargoRecordConverter)
            );
            var gen = builder.GetILGenerator();
            // Find ctor with most parameters.
            var ctor = modelType.GetConstructors()
                .Where(m => m.IsPublic)
                .Aggregate((x, y) => x.GetParameters().Length > y.GetParameters().Length ? x : y);
            var assignedFields = new HashSet<string>();
            // Assignment from ctor (for anonymous types)
            foreach (var p in ctor.GetParameters())
            {
                Debug.Assert(p.Name != null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldstr, p.Name);
                gen.EmitCall(OpCodes.Callvirt, dictIndexer, null);
                // TODO associate ctor param with property to get CargoListAttribute.
                EmitDeserializeValue(gen, p.ParameterType, null);
                assignedFields.Add(p.Name);
            }
            gen.Emit(OpCodes.Newobj, ctor);
            var assignableProps = modelType.GetProperties()
                .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && !assignedFields.Contains(p.Name))
                .ToList();
            // Assignment with setter
            if (assignableProps.Count > 0)
            {
                var modelLocal = gen.DeclareLocal(modelType).LocalIndex;
                var jTokenLocal = gen.DeclareLocal(typeof(JToken)).LocalIndex;
                gen.Emit(OpCodes.Stloc, modelLocal);
                foreach (var p in assignableProps)
                {
                    Debug.Assert(p.SetMethod != null);

                    var label = gen.DefineLabel();

                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldstr, p.Name);
                    gen.Emit(OpCodes.Ldloca_S, jTokenLocal);
                    gen.EmitCall(OpCodes.Callvirt, dictTryGetValue, null);
                    gen.Emit(OpCodes.Brfalse_S, label);

                    gen.Emit(OpCodes.Ldloc_S, modelLocal);
                    gen.Emit(OpCodes.Ldloc_S, jTokenLocal);
                    EmitDeserializeValue(gen, p.PropertyType, p.GetCustomAttribute<CargoListAttribute>());
                    gen.EmitCall(OpCodes.Callvirt, p.SetMethod, null);

                    gen.MarkLabel(label);
                }
                gen.Emit(OpCodes.Ldloc_S, modelLocal);
            }
            gen.Emit(OpCodes.Ret);
            des = (Func<IReadOnlyDictionary<string, JToken>, object>)builder.CreateDelegate(typeof(Func<IReadOnlyDictionary<string, JToken>, object>));
            cachedDeserializers.TryAdd(modelType, des);
        }
        return des(record.ToDictionary(r => r.Key.Name, r => r.Value));
    }

    private static class ValueConverters
    {

        private static readonly MethodInfo deserializeValueMethod = typeof(ValueConverters).GetMethod(nameof(DeserializeValue))!;

        private static readonly MethodInfo deserializeNullableValueMethod = typeof(ValueConverters).GetMethod(nameof(DeserializeNullableValue))!;

        private static readonly Dictionary<Type, MethodInfo> wellKnownValueDeserializers
            = typeof(ValueConverters)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.IsGenericMethod
                            && m.Name.StartsWith("Deserialize", StringComparison.Ordinal))
                .ToDictionary(m => m.ReturnType);

        public static MethodInfo GetValueDeserializer(Type valueType)
        {
            if (wellKnownValueDeserializers.TryGetValue(valueType, out var m))
                return m;
            var nullableUnderlyingType = Nullable.GetUnderlyingType(valueType);
            if (nullableUnderlyingType != null) return deserializeNullableValueMethod.MakeGenericMethod(nullableUnderlyingType);
            return deserializeValueMethod.MakeGenericMethod(valueType);
        }

#if NETSTANDARD2_1
            public static T DeserializeValue<T>(JToken? value) => value == null ? default! : value.ToObject<T>();
#else
        public static T? DeserializeValue<T>(JToken? value) => value == null ? default : value.ToObject<T>();
#endif

        public static bool IsNullableNull(JToken value)
        {
            if (value == null) return true;
            if (value.Type == JTokenType.Null) return true;
            if (value.Type == JTokenType.Undefined) return true;
            if (value.Type == JTokenType.String)
            {
                var s = (string)value;
                return s.Length == 0 || s.Equals("null", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public static T? DeserializeNullableValue<T>(JToken value) where T : struct
        {
            if (IsNullableNull(value)) return null;
            return value.ToObject<T>();
        }

        public static string DeserializeString(JToken value) => (string)value;

        public static int DeserializeInt32(JToken value) => (int)value;

        public static long DeserializeInt64(JToken value) => (long)value;

        public static float DeserializeFloat(JToken value) => (float)value;

        public static double DeserializeDouble(JToken value) => (double)value;

        public static decimal DeserializeDecimal(JToken value) => (decimal)value;

        public static bool DeserializeBoolean(JToken value)
        {
            if (value.Type == JTokenType.Integer)
            {
                var v = (int)value;
                return v != 0;
            }
            if (value.Type == JTokenType.String)
            {
                var s = (string)value;
                if (s.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("-1", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (s.Equals("0", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v != 0;
            }
            return (bool)value;
        }

        public static DateTime DeserializeDateTime(JToken value) => (DateTime)value;

        public static DateTimeOffset DeserializeDateTimeOffset(JToken value) => (DateTimeOffset)value;

        public static int? DeserializeNullableInt32(JToken value) => IsNullableNull(value) ? (int?)null : (int)value;

        public static long? DeserializeNullableInt64(JToken value) => IsNullableNull(value) ? (long?)null : (long)value;

        public static float? DeserializeNullableFloat(JToken value) => IsNullableNull(value) ? (float?)null : (float)value;

        public static double? DeserializeNullableDouble(JToken value) => IsNullableNull(value) ? (double?)null : (double)value;

        public static decimal? DeserializeNullableDecimal(JToken value) => IsNullableNull(value) ? (decimal?)null : (decimal)value;

        public static bool? DeserializeNullableBoolean(JToken value) => IsNullableNull(value) ? (bool?)null : DeserializeBoolean(value);

        public static DateTime? DeserializeNullableDateTime(JToken value) => IsNullableNull(value) ? (DateTime?)null : (DateTime)value;

        public static DateTimeOffset? DeserializeNullableDateTimeOffset(JToken value)
            => IsNullableNull(value) ? (DateTimeOffset?)null : (DateTimeOffset)value;

    }

}