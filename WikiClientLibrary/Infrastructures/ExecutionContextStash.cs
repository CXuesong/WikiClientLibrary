using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace WikiClientLibrary.Infrastructures
{

    // c.f. https://github.com/dotnet/runtime/issues/47951
    // c.f. https://github.com/dotnet/runtime/issues/47802
    /// <summary>
    /// This type is infrastructure of WCL and is not intended to be used directly in your own code.
    /// Restores the execution context by <c>await</c>ing on this structure.
    /// </summary>
    /// <remarks>
    /// This helper is for correctly restoring execution context (and <see cref="AsyncLocal{T}"/>) after <c>yield return</c>.
    /// <c>Microsoft.Extension.Logging</c> depends on that for scoped logging.
    /// </remarks>
    public readonly struct ExecutionContextStash : IDisposable
    {

        private readonly ExecutionContext executionContext;

        public static ExecutionContextStash Capture()
        {
            return new ExecutionContextStash(ExecutionContext.Capture());
        }

        private ExecutionContextStash(ExecutionContext context)
        {
            this.executionContext = context;
        }

        /// <summary>
        /// Gets an awaiter to <c>await</c> in order to enter the Captured execution context.
        /// </summary>
        public void RestoreExecutionContext()
        {
            if (executionContext == null) return;
            // Restore execution context inline.
#if BCL_FEATURE_EXECUTION_CONTEXT_RESTORE
            // On .NET 5+, restores execution context with public API.
            ExecutionContext.Restore(executionContext);
#else
            // Otherwise, invoke our hacky delegate.
            restoreExecutionContext(executionContext);
#endif
        }

        /// <summary>Calls <see cref="RestoreExecutionContext"/>.</summary>
        public void Dispose() => RestoreExecutionContext();

#if !BCL_FEATURE_EXECUTION_CONTEXT_RESTORE
        private static readonly Action<ExecutionContext> restoreExecutionContext = BuildRestoreExecutionContextDelegate();

        private static Action<ExecutionContext> BuildRestoreExecutionContextDelegate()
        {
            // You feel like you're going to have a bad time.

            // On .NET Standard 2.1, there is no ExecutionContext.Restore(ExecutionContext) public API.
            // Cannot leverage ExecutionContext.Run and Awaiter.OnComplete impl here as CPS transformation done by C# compiler does not give us enough freedom.
            // `AsyncTaskMethodBuilder<TResult>.GetStateMachineBox` is always capturing current ExecutionContext and
            // restoring it *inside* OnComplete callback thunk. This behavior cannot be disabled.

            // 1) Try to retrieve public/internal API (if exists) during runtime.
            // Mono 6.4 https://github.com/mono/mono/blob/mono-6.4.0.198/mcs/class/referencesource/mscorlib/system/threading/executioncontext.cs#L117
            var restoreMethod = typeof(ExecutionContext).GetMethod("Restore",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any,
                new[] { typeof(ExecutionContext) },
                null);
            if (restoreMethod != null)
            {
                var dm = new DynamicMethod("proxy$ExecutionContext.Restore",
                    typeof(void), new[] { typeof(ExecutionContext) },
                    typeof(ExecutionContextStash),
                    skipVisibility: true);
                var gen = dm.GetILGenerator();
                gen.Emit(OpCodes.Ldarg_0);
                gen.EmitCall(OpCodes.Call, restoreMethod, null);
                gen.Emit(OpCodes.Ret);

                return (Action<ExecutionContext>)dm.CreateDelegate(typeof(Action<ExecutionContext>));
            }

            // ExecutionContext.Restore does not exist.
            // 2) Try to leverage non-public API RestoreChangedContextToThread.
            // .NET Core 3.x.
            var restoreChangedContextToThreadMethod = typeof(ExecutionContext).GetMethod("RestoreChangedContextToThread",
                BindingFlags.Static | BindingFlags.NonPublic, null, CallingConventions.Any,
                new[] { typeof(Thread), typeof(ExecutionContext), typeof(ExecutionContext) },
                null);
            if (restoreChangedContextToThreadMethod != null)
            {
                var dm = new DynamicMethod("proxy$ExecutionContext.RestoreChangedContextToThread",
                    typeof(void), new[] { typeof(ExecutionContext) },
                    typeof(ExecutionContextStash),
                    skipVisibility: true);
                var gen = dm.GetILGenerator();
                var label1 = gen.DefineLabel();
                var currentContextLocal = gen.DeclareLocal(typeof(ExecutionContext));

                // currentContext = ExecutionContext.Capture()
                gen.EmitCall(OpCodes.Call, typeof(ExecutionContext).GetMethod(nameof(ExecutionContext.Capture), Array.Empty<Type>()), null);
                gen.Emit(OpCodes.Dup);
                gen.Emit(OpCodes.Stloc, currentContextLocal.LocalIndex);
                // if (currentContext != arg0) {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Beq_S, label1);

                // RestoreChangedContextToThread(Thread currentThread, ExecutionContext? contextToRestore, ExecutionContext? currentContext)
                gen.EmitCall(OpCodes.Call, typeof(Thread).GetProperty(nameof(Thread.CurrentThread)).GetMethod, null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldloc, currentContextLocal.LocalIndex);
                gen.EmitCall(OpCodes.Call, restoreChangedContextToThreadMethod, null);
                gen.Emit(OpCodes.Ret);

                // }
                gen.MarkLabel(label1);
                gen.Emit(OpCodes.Ret);

                return (Action<ExecutionContext>)dm.CreateDelegate(typeof(Action<ExecutionContext>));
            }

            // We can do nothing to help. Sorry.
            throw new PlatformNotSupportedException("Your current .NET CLR does not support restoring ExecutionContext. " +
                                                    "Please file an issue on CXuesong/WikiClientLibrary on github.");
        }
#endif

    }
}
