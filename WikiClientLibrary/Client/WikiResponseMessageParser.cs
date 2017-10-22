using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary.Client
{

    /// <summary>
    /// Provides methods for parsing <see cref="HttpResponseMessage"/> into appropriate type of data
    /// that can be handled by the subsequent procedure.
    /// </summary>
    /// <remarks>
    /// <para>It's suggested to derive your response message parser classes from <see cref="WikiResponseMessageParser{T}"/>,
    /// instead of imeplementing this interface directly.</para>
    /// <para>For the role this interface plays in invoking wiki API, see <see cref="IWikiClient.InvokeAsync"/>.</para>
    /// </remarks>
    public interface IWikiResponseMessageParser
    {

        /// <summary>
        /// Parses the specified HTTP response message.
        /// </summary>
        /// <param name="response">The HTTP response message to parse.</param>
        /// <param name="context">The parsing context.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="response"/> or <paramref name="context"/> is <c>null</c>.</exception>
        /// <exception cref="Exception">An exception occurred when parsing the response. Setting <c>context.NeedRetry</c> to <c>true</c> to request for a retry.</exception>
        /// <returns>The task that will return the parsed value.</returns>
        /// <remarks>
        /// <para>The implementation should check <see cref="HttpResponseMessage.StatusCode"/> first, then parse the content.</para>
        /// <para>If <paramref name="context"/>.<see cref="WikiResponseParsingContext.NeedRetry"/> is set to <c>true</c>,
        /// then the invoker should attempt to retry first, even if the returned <see cref="Task"/> throws an exception.
        /// However, the caller may regardless throw the exception if it decides that the request cannot be retried.</para>
        /// </remarks>
        Task<object> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context);

    }

    /// <summary>
    /// A strong-typed base class for implementing <see cref="IWikiResponseMessageParser"/>.
    /// </summary>
    /// <typeparam name="T">The type of parsed response value.</typeparam>
    /// <remarks>
    /// <para>It's suggested you start from derive from this class to implement <see cref="IWikiResponseMessageParser"/>.</para>
    /// <para>For the role this interface plays in invoking wiki API, see <see cref="IWikiClient.InvokeAsync"/>.</para>
    /// </remarks>
    public abstract class WikiResponseMessageParser<T> : IWikiResponseMessageParser
    {

        /// <summary>
        /// Parses the specified HTTP response message.
        /// </summary>
        /// <param name="response">The HTTP response message to parse.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>A strongly-typed object containing the desired response.</returns>
        /// <remarks>For general guidance on how this method should be implemented, see <see cref="IWikiResponseMessageParser.ParseResponseAsync"/>.</remarks>
        public abstract Task<T> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context);

        /// <inheritdoc />
        async Task<object> IWikiResponseMessageParser.ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
        {
            return await ParseResponseAsync(response, context);
        }
    }
}
