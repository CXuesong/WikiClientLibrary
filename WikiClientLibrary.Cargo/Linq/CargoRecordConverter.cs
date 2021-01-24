using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Cargo.Linq
{

    public interface ICargoRecordConverter
    {

        /// <summary>
        /// Converts JSON record object from MediaWiki Cargo API response into CLR model type.
        /// </summary>
        object DeserializeRecord(IReadOnlyDictionary<string, JToken> record, Type modelType);

    }

    public class CargoRecordConverter : ICargoRecordConverter
    {

        private static readonly MethodInfo dictIndexer = typeof(IReadOnlyDictionary<string, JToken>)
            .GetRuntimeProperties()
            .First(p => p.GetIndexParameters().Length > 0)
            .GetMethod;

        private static readonly MethodInfo dictTryGetValue = typeof(IReadOnlyDictionary<string, JToken>)
            .GetRuntimeMethod(nameof(IReadOnlyDictionary<string, JToken>.TryGetValue),
                new[] { typeof(string), typeof(JToken).MakeByRefType() });

        private static readonly MethodInfo jtokenToObject = typeof(JToken)
            .GetRuntimeMethod(nameof(JToken.ToObject), Type.EmptyTypes);

        private readonly ConcurrentDictionary<Type, Func<IReadOnlyDictionary<string, JToken>, object>> cachedDeserializers =
            new ConcurrentDictionary<Type, Func<IReadOnlyDictionary<string, JToken>, object>>();

        public virtual object DeserializeRecord(IReadOnlyDictionary<string, JToken> record, Type modelType)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (modelType == null) throw new ArgumentNullException(nameof(modelType));
            if (!cachedDeserializers.TryGetValue(modelType, out var des))
            {
                var builder = new DynamicMethod("DeserializeRecordImpl$" + modelType, modelType, new[] { typeof(IReadOnlyDictionary<string, JToken>) });
                var gen = builder.GetILGenerator();
                var ctor = modelType.GetTypeInfo().DeclaredConstructors
                    .Where(m => m.IsPublic)
                    .Aggregate((x, y) => x.GetParameters().Length > y.GetParameters().Length ? x : y);
                var assignedFields = new HashSet<string>();
                // Assignment from ctor
                foreach (var p in ctor.GetParameters())
                {
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldstr, p.Name);
                    gen.EmitCall(OpCodes.Callvirt, dictIndexer, null);
                    gen.EmitCall(OpCodes.Callvirt, jtokenToObject.MakeGenericMethod(p.ParameterType), null);
                    assignedFields.Add(p.Name);
                }
                gen.Emit(OpCodes.Newobj, ctor);
                var assignableProps = modelType.GetRuntimeProperties()
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
                        var label = gen.DefineLabel();

                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Ldstr, p.Name);
                        gen.Emit(OpCodes.Ldloca_S, jTokenLocal);
                        gen.EmitCall(OpCodes.Callvirt, dictTryGetValue, null);
                        gen.Emit(OpCodes.Brfalse_S, label);

                        gen.Emit(OpCodes.Ldloc_S, modelLocal);
                        gen.Emit(OpCodes.Ldloc_S, jTokenLocal);
                        gen.EmitCall(OpCodes.Callvirt, jtokenToObject.MakeGenericMethod(p.PropertyType), null);
                        gen.EmitCall(OpCodes.Callvirt, p.SetMethod, null);

                        gen.MarkLabel(label);
                    }
                    gen.Emit(OpCodes.Ldloc_S, modelLocal);
                }
                gen.Emit(OpCodes.Ret);
                des = (Func<IReadOnlyDictionary<string, JToken>, object>)builder.CreateDelegate(typeof(Func<IReadOnlyDictionary<string, JToken>, object>));
                cachedDeserializers.TryAdd(modelType, des);
            }
            return des(record);
        }

    }

}
