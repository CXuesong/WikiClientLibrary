using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides methods to carry out MediaWiki or other kinds of API invocation.
    /// </summary>
    public interface IWikiClient
    {
        /// <summary>
        /// Performs API invocation on the specified endpoint and gets parsed result.
        /// </summary>
        /// <param name="endPointUrl">The API endpoint URL.</param>
        /// <param name="request">The request message.</param>
        /// <param name="responseParser">The parser that checks and parses the API response into the desired CLR object.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <returns>The parsed response value. The acutal object type depends on the <paramref name="responseParser"/>.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="endPointUrl"/>, <paramref name="request"/>, or <paramref name="responseParser"/> is <c>null</c>.</exception>
        /// <exception cref="Exception">Other <see cref="IWikiResponseMessageParser"/>-specified exceptions.</exception>
        /// <remarks>
        /// The implementation of this method involves
        /// <list type="bullet">
        /// <item><description>Generating <see cref="HttpRequestMessage"/> from <paramref name="request"/>;</description></item>
        /// <item><description>Transmitting <see cref="HttpRequestMessage"/>, and gets the <see cref="HttpResponseMessage"/>;</description></item>
        /// <item><description>Parsing the <see cref="HttpResponseMessage"/> using <paramref name="responseParser"/> (see <see cref="IWikiResponseMessageParser.ParseResponseAsync"/>);</description></item>
        /// <item><description>Retrying if possible;</description></item>
        /// <item><description>Returning the parsed result, or throwing an exception.</description></item>
        /// </list>
        /// </remarks>
        Task<object> InvokeAsync(string endPointUrl, WikiRequestMessage request,
            IWikiResponseMessageParser responseParser, CancellationToken cancellationToken);

    }

}
