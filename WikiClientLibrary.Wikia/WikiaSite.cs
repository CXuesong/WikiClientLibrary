using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikia
{
    /// <summary>
    /// The encapsulated Wikia site endpoint.
    /// </summary>
    public partial class WikiaSite
    {

        private readonly WikiaSiteOptions options;

        public WikiaSite(IWikiClient wikiClient, WikiaSiteOptions options)
        {
            WikiClient = wikiClient ?? throw new ArgumentNullException(nameof(wikiClient));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Gets the API client used to perform the requests.
        /// </summary>
        public IWikiClient WikiClient { get; }

        /// <summary>
        /// Invokes <c>index.php</c> call.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="responseParser">The parser used to parse the response.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The parsed JSON root of response.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
        /// <remarks>To use non-publicized Wikia ajax APIs, invoke <c>index.php</c> with field <c>action=ajax</c>.</remarks>
        public async Task<JToken> InvokeMediaWikiScriptAsync(WikiRequestMessage request, IWikiResponseMessageParser responseParser,
            CancellationToken cancellationToken)
        {
            var result = (JToken)await WikiClient.InvokeAsync(options.ScriptUrl, request, responseParser, cancellationToken);
            return result;
        }

        /// <summary>
        /// Invokes nirvana API call.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="responseParser">The parser used to parse the response.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
        /// <returns>The parsed JSON root of response.</returns>
        public async Task<JToken> InvokeNirvanaAsync(WikiRequestMessage request, IWikiResponseMessageParser responseParser, CancellationToken cancellationToken)
        {
            var result = (JToken)await WikiClient.InvokeAsync(options.NirvanaEndPointUrl, request, responseParser, cancellationToken);
            return result;
        }

        /// <summary>
        /// Invokes Wikia API v1 call.
        /// </summary>
        /// <param name="relativeUri">The URI relative to <see cref="WikiaSiteOptions.WikiaApiRootUrl"/>. It often starts with a slash.</param>
        /// <param name="request">The request message.</param>
        /// <param name="responseParser">The parser used to parse the response.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
        /// <returns>The parsed JSON root of response.</returns>
        public async Task<JToken> InvokeWikiaApiAsync(string relativeUri, WikiRequestMessage request, IWikiResponseMessageParser responseParser,
            CancellationToken cancellationToken)
        {
            var result = (JToken)await WikiClient.InvokeAsync(options.WikiaApiRootUrl, request, responseParser, cancellationToken);
            return result;
        }

    }
}
