namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// Provides helper methods for <see cref="IWikiClientAsyncInitialization"/>.
/// </summary>
public static class AsyncInitializationHelper
{

    /// <summary>
    /// Ensures the instance has been initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">The instance has not been initialized.</exception>
    public static void EnsureInitialized(IWikiClientAsyncInitialization obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        var task = obj.Initialization;
        if (task == null) return;
        if (task.Status == TaskStatus.RanToCompletion) return;
        EnsureInitialized(obj.GetType(), task);
    }

    /// <summary>
    /// Ensures the specified asynchronous initialization task of the instance has completed successfully.
    /// </summary>
    /// <exception cref="InvalidOperationException">The instance has not been initialized.</exception>
    public static void EnsureInitialized(Type objectType, Task initializationTask)
    {
        if (objectType == null) throw new ArgumentNullException(nameof(objectType));
        if (initializationTask == null) return;
        if (initializationTask.Status == TaskStatus.RanToCompletion) return;
        var name = objectType.Name;
        throw initializationTask.Status switch
        {
            TaskStatus.Canceled => new InvalidOperationException(string.Format(Prompts.ExceptionAsyncInitCancelled1, name)),
            TaskStatus.Faulted => new InvalidOperationException(
                string.Format(Prompts.ExceptionAsyncInitFaulted2, name, initializationTask.Exception),
                initializationTask.Exception),
            _ => new InvalidOperationException(string.Format(Prompts.ExceptionAsyncInitNotComplete1, name)),
        };
    }

}
