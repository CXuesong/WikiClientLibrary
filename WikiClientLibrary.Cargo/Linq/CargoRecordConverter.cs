using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using WikiClientLibrary.Cargo.Schema.DataAnnotations;

namespace WikiClientLibrary.Cargo.Linq;

public interface ICargoRecordConverter
{

    /// <summary>
    /// Converts JSON record object from MediaWiki Cargo API response into CLR model type.
    /// </summary>
    object DeserializeRecord(IReadOnlyDictionary<MemberInfo, JsonNode> record, Type modelType);

}

public class CargoRecordConverter : ICargoRecordConverter
{

    private static readonly MethodInfo dictIndexer
        = typeof(IReadOnlyDictionary<string, JsonNode>).GetProperty("Item")!.GetMethod!;

    private static readonly MethodInfo dictTryGetValue
        = typeof(IReadOnlyDictionary<string, JsonNode>).GetMethod(nameof(IReadOnlyDictionary<string, JsonNode>.TryGetValue))!;

    private static readonly MethodInfo deserializeCollectionMethod = typeof(CargoRecordConverter)
        .GetMethod(nameof(DeserializeCollection), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo deserializeStringCollectionMethod = typeof(CargoRecordConverter)
        .GetMethod(nameof(DeserializeStringCollection), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly ConcurrentDictionary<Type, Func<IReadOnlyDictionary<string, JsonNode>, object>> cachedDeserializers = new();

    [return: NotNullIfNotNull("value")]
    private static IList<T>? DeserializeCollection<T>(JsonNode? value, string separator)
    {
        var values = (string)value;
        if (values == null) return default;
        if (values.Length == 0) return Array.Empty<T>();
        return values.Split(new[] { separator }, StringSplitOptions.None)
            .Select(i => (T)Convert.ChangeType(i, typeof(T), CultureInfo.InvariantCulture))
            .ToList()!;
    }

    [return: NotNullIfNotNull("value")]
    private static IList<string>? DeserializeStringCollection(JsonNode? value, string separator)
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

    public virtual object DeserializeRecord(IReadOnlyDictionary<MemberInfo, JsonNode> record, Type modelType)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (modelType == null) throw new ArgumentNullException(nameof(modelType));
        if (!cachedDeserializers.TryGetValue(modelType, out var des))
        {
            var builder = new DynamicMethod(
                typeof(CargoRecordConverter) + "$DeserializeRecordImpl[" + modelType + "]",
                modelType,
                new[] { typeof(IReadOnlyDictionary<string, JsonNode>) },
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
                var jTokenLocal = gen.DeclareLocal(typeof(JsonNode)).LocalIndex;
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
            des = (Func<IReadOnlyDictionary<string, JsonNode>, object>)builder.CreateDelegate(
                typeof(Func<IReadOnlyDictionary<string, JsonNode>, object>));
            cachedDeserializers.TryAdd(modelType, des);
        }
        return des(record.ToDictionary(r => r.Key.Name, r => r.Value));
    }

    private static class ValueConverters
    {

        private static readonly MethodInfo deserializeValueMethod = typeof(ValueConverters).GetMethod(nameof(DeserializeValue))!;

        private static readonly MethodInfo deserializeNullableValueMethod =
            typeof(ValueConverters).GetMethod(nameof(DeserializeNullableValue))!;

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

        public static T? DeserializeValue<T>(JsonNode? value) => value == null ? default : value.Deserialize<T>();

        public static bool IsNullableNull(JsonNode token)
        {
            if (token == null) return true;
            if (token is not JsonValue value) return false;

            if (value.GetValueKind() == JsonValueKind.Null) return true;
            if (value.TryGetValue(out string? str))
            {
                return str == "" || str.Equals("null", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public static T? DeserializeNullableValue<T>(JsonNode value) where T : struct
        {
            if (IsNullableNull(value)) return null;
            return value.Deserialize<T>();
        }

        public static string? DeserializeString(JsonNode value) => (string?)value;

        private static Exception CreateInvalidCastException(Type targetType)
            => new InvalidOperationException($"Cannot cast the JSON node into {targetType}.");

        public static int DeserializeInt32(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out int i32)) return i32;
            if (value.TryGetValue(out string? str)) return Convert.ToInt32(str);
            throw CreateInvalidCastException(typeof(int));
        }

        public static long DeserializeInt64(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out long i64)) return i64;
            if (value.TryGetValue(out string? str)) return Convert.ToInt64(str);
            throw CreateInvalidCastException(typeof(long));
        }

        public static float DeserializeFloat(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out float f)) return f;
            if (value.TryGetValue(out string? str)) return Convert.ToSingle(str);
            throw CreateInvalidCastException(typeof(float));
        }

        public static double DeserializeDouble(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out double d)) return d;
            if (value.TryGetValue(out string? str)) return Convert.ToDouble(str);
            throw CreateInvalidCastException(typeof(double));
        }

        public static decimal DeserializeDecimal(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out decimal dec)) return dec;
            if (value.TryGetValue(out string? str)) return Convert.ToDecimal(str);
            throw CreateInvalidCastException(typeof(decimal));
        }

        public static bool DeserializeBoolean(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out int i32)) return i32 != 0;
            if (value.TryGetValue(out string? str))
            {
                if (str.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || str.Equals("-1", StringComparison.OrdinalIgnoreCase)
                    || str.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (str.Equals("0", StringComparison.OrdinalIgnoreCase)
                    || str.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v != 0;
            }
            return (bool)value;
        }

        public static DateTime DeserializeDateTime(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out DateTime dt)) return dt;
            if (value.TryGetValue(out string? str)) return Convert.ToDateTime(str);
            throw CreateInvalidCastException(typeof(DateTime));
        }

        public static DateTimeOffset DeserializeDateTimeOffset(JsonNode node)
        {
            var value = node.AsValue();
            if (value.TryGetValue(out DateTimeOffset dto)) return dto;
            if (value.TryGetValue(out string? str)) return DateTimeOffset.Parse(str);
            throw CreateInvalidCastException(typeof(DateTimeOffset));
        }

        public static int? DeserializeNullableInt32(JsonNode value) => IsNullableNull(value) ? (int?)null : (int)value;

        public static long? DeserializeNullableInt64(JsonNode value) => IsNullableNull(value) ? (long?)null : (long)value;

        public static float? DeserializeNullableFloat(JsonNode value) => IsNullableNull(value) ? (float?)null : (float)value;

        public static double? DeserializeNullableDouble(JsonNode value) => IsNullableNull(value) ? (double?)null : (double)value;

        public static decimal? DeserializeNullableDecimal(JsonNode value) => IsNullableNull(value) ? (decimal?)null : (decimal)value;

        public static bool? DeserializeNullableBoolean(JsonNode value) => IsNullableNull(value) ? (bool?)null : DeserializeBoolean(value);

        public static DateTime? DeserializeNullableDateTime(JsonNode value) => IsNullableNull(value) ? (DateTime?)null : (DateTime)value;

        public static DateTimeOffset? DeserializeNullableDateTimeOffset(JsonNode value)
            => IsNullableNull(value) ? (DateTimeOffset?)null : (DateTimeOffset)value;

    }

}
