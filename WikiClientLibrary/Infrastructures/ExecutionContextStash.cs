namespace WikiClientLibrary.Infrastructures;

// c.f. https://github.com/dotnet/runtime/issues/47951
// c.f. https://github.com/dotnet/runtime/issues/47802
/// <summary>
/// This type is infrastructure of WCL and is not intended to be used directly in your own code.
/// Restores the execution context by calling <see cref="Dispose"/> on this structure.
/// </summary>
/// <remarks>
/// This helper is for correctly restoring execution context (and <see cref="AsyncLocal{T}"/>) after <c>yield return</c>.
/// <c>Microsoft.Extension.Logging</c> depends on that for scoped logging.
/// </remarks>
public readonly struct ExecutionContextStash : IDisposable
{

    private readonly ExecutionContext? executionContext;

    public static ExecutionContextStash Capture()
    {
        return new ExecutionContextStash(ExecutionContext.Capture());
    }

    private ExecutionContextStash(ExecutionContext? context)
    {
        this.executionContext = context;
    }

    /// <summary>
    /// Restores the captured execution context.
    /// </summary>
    public void RestoreExecutionContext()
    {
        if (executionContext == null) return;
        // Restore execution context inline.
        ExecutionContext.Restore(executionContext);
    }

    /// <summary>Calls <see cref="RestoreExecutionContext"/>.</summary>
    public void Dispose() => RestoreExecutionContext();

}
