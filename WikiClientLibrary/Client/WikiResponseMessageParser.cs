namespace WikiClientLibrary.Client;

/// <summary>
/// Provides methods for parsing <see cref="HttpResponseMessage"/> into appropriate type of data
/// that can be handled by the subsequent procedure.
/// </summary>
/// <remarks>
/// <para>It's suggested to derive your response message parser classes from <see cref="WikiResponseMessageParser{T}"/>,
/// instead of implementing this interface directly.</para>
/// <para>For the role this interface plays in invoking wiki API, see <see cref="IWikiClient.InvokeAsync"/>.</para>
/// </remarks>
/// <typeparam name="T">The type of parsed response.</typeparam>
public interface IWikiResponseMessageParser<out T>
{

    /// <summary>
    /// Parses the specified HTTP response message.
    /// </summary>
    /// <param name="response">The HTTP response message to parse.</param>
    /// <param name="context">The parsing context.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="response"/> or <paramref name="context"/> is <c>null</c>.</exception>
    /// <exception cref="Exception">An exception occurred when parsing the response. Setting <c>context.NeedRetry</c> to <c>true</c> to request for a retry.</exception>
    /// <returns>The task that will return the parsed value. The parsed value should be able to be converted to <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>The implementation should check <see cref="HttpResponseMessage.StatusCode"/> first, then parse the content.</para>
    /// <para>If <paramref name="context"/>.<see cref="WikiResponseParsingContext.NeedRetry"/> is set to <c>true</c>,
    /// then the invoker should attempt to retry first, even if the returned <see cref="Task"/> throws an exception.
    /// However, the caller may regardless throw the exception if it decides that the request cannot be retried.</para>
    /// </remarks>
    Task<object> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context);

}

/// <summary>
/// A strong-typed base class for implementing <see cref="IWikiResponseMessageParser{T}"/>.
/// </summary>
/// <typeparam name="T">The type of parsed response value.</typeparam>
/// <remarks>
/// <para>It's suggested you start from derive from this class to implement <see cref="IWikiResponseMessageParser{T}"/>.</para>
/// <para>For the role this interface plays in invoking wiki API, see <see cref="IWikiClient.InvokeAsync{T}"/>.</para>
/// </remarks>
public abstract class WikiResponseMessageParser<T> : IWikiResponseMessageParser<T> where T : notnull
{

    /// <summary>
    /// Parses the specified HTTP response message.
    /// </summary>
    /// <param name="response">The HTTP response message to parse.</param>
    /// <param name="context">The parsing context.</param>
    /// <returns>A strongly-typed object containing the desired response.</returns>
    /// <remarks>For general guidance on how this method should be implemented, see <see cref="IWikiResponseMessageParser{T}.ParseResponseAsync"/>.</remarks>
    public abstract Task<T> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context);

    /// <inheritdoc />
    async Task<object> IWikiResponseMessageParser<T>.ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
    {
        return await ParseResponseAsync(response, context);
    }

}
