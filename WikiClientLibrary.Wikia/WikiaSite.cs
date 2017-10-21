using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikia
{
    /// <summary>
    /// The encapsulated Wikia site endpoint.
    /// </summary>
    public class WikiaSite : IWikiClientLoggable
    {

        private readonly WikiaSiteOptions options;
        private ILogger _Logger = NullLogger.Instance;

        public WikiaSite(WikiSite wikiSite)
        {
            if (wikiSite == null) throw new ArgumentNullException(nameof(wikiSite));
            WikiClient = wikiSite.WikiClient;
            options = new WikiaSiteOptions(wikiSite.SiteInfo);
        }

        public WikiaSite(IWikiClient wikiClient, string siteRootUrl)
        {
            WikiClient = wikiClient ?? throw new ArgumentNullException(nameof(wikiClient));
            options = new WikiaSiteOptions(siteRootUrl);
        }

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
        /// Invokes <c>index.php</c> call with <c>action=ajax</c> query.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="responseParser">The parser used to parse the response.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The parsed JSON root of response.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
        /// <remarks>This method will automatically add <c>action=ajax</c> field in the request.</remarks>
        public async Task<JToken> InvokeWikiaAjaxAsync(WikiRequestMessage request,
            IWikiResponseMessageParser responseParser, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (responseParser == null) throw new ArgumentNullException(nameof(responseParser));
            var localRequest = request;
            if (request is WikiaQueryRequestMessage queryRequest)
            {
                var fields = new List<KeyValuePair<string, object>>(queryRequest.Fields.Count + 1)
                {
                    new KeyValuePair<string, object>("action", "ajax")
                };
                fields.AddRange(queryRequest.Fields);
                localRequest = new WikiaQueryRequestMessage(request.Id, fields);
            }
            Logger.LogDebug("Invoking Wikia Ajax v1: {Request}", localRequest);
            var result = (JToken)await WikiClient.InvokeAsync(options.ScriptUrl, localRequest,
                responseParser, cancellationToken);
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
        /// <seealso cref="WikiaSiteOptions.NirvanaEndPointUrl"/>
        public async Task<JToken> InvokeNirvanaAsync(WikiRequestMessage request, IWikiResponseMessageParser responseParser, CancellationToken cancellationToken)
        {
            Logger.LogDebug("Invoking Nirvana API: {Request}", request);
            var result = (JToken)await WikiClient.InvokeAsync(options.NirvanaEndPointUrl, request, responseParser, cancellationToken);
            return result;
        }

        /// <inheritdoc cref="InvokeWikiaApiAsync(string,WikiRequestMessage,IWikiResponseMessageParser,CancellationToken)"/>
        /// <remarks>This overload uses <see cref="WikiaJsonResonseParser"/> to parse the response.</remarks>
        public Task<JToken> InvokeWikiaApiAsync(string relativeUri, WikiRequestMessage request, CancellationToken cancellationToken)
        {
            return InvokeWikiaApiAsync(relativeUri, request, WikiaJsonResonseParser.Default, cancellationToken);
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
            Logger.LogDebug("Invoking Wikia API v1: {Request}", request);
            var result = (JToken)await WikiClient.InvokeAsync(options.WikiaApiRootUrl + relativeUri,
                request, responseParser, cancellationToken);
            return result;
        }

        /// <inheritdoc />
        public ILogger Logger
        {
            get => _Logger;
            set => _Logger = value ?? NullLogger.Instance;
        }
    }
}
