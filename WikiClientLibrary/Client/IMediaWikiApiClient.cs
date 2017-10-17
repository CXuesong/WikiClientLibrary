using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides methods for transporting MediaWiki API invocation.
    /// </summary>
    public interface IMediaWikiApiClient
    {
        
        /// <summary>
        /// Invokes MediaWiki API and gets JSON result.
        /// </summary>
        /// <param name="endPointUrl">The API endpoint URL.</param>
        /// <param name="message">The request message.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="OperationFailedException">There is <c>error</c> node in returned JSON and error is unhandled.</exception>
        Task<JToken> GetJsonAsync(string endPointUrl, WikiRequestMessage message, CancellationToken cancellationToken);

    }
}
