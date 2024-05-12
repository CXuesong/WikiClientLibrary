namespace WikiClientLibrary;

/// <summary>
/// Provides properties to expose the asynchronous initialization status of an instance.
/// </summary>
/// <remarks>
/// When instantiating types implementing this interface, you need to wait for
/// <see cref="Initialization"/> to complete before you access any other members of the type.
/// For more about asynchronous initialization, see the blog post
/// <a href="http://blog.stephencleary.com/2013/01/async-oop-2-constructors.html">Async OOP 2: Constructors</a>.
/// </remarks>
public interface IWikiClientAsyncInitialization
{

    /// <summary>
    /// A task that indicates the asynchronous initialization status of this instance.
    /// </summary>
    Task Initialization { get; }

}
