using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides basic operations for MediaWiki API via HTTP(S).
    /// </summary>
    public partial class WikiClient : WikiClientBase
    {
        /// <summary>
        /// The User Agent of Wiki Client Library.
        /// </summary>
        public const string WikiClientUserAgent = "WikiClientLibrary/0.6 (.NET Portable; http://github.com/cxuesong/WikiClientLibrary)";

        #region Configurations

        private string _ClientUserAgent;
        private readonly HttpClientHandler _HttpClientHandler;

        /// <summary>
        /// User Agent for client-side application.
        /// </summary>
        public string ClientUserAgent
        {
            get { return _ClientUserAgent; }
            set
            {
                if (_ClientUserAgent != value)
                {
                    var ua = HttpClient.DefaultRequestHeaders.UserAgent;
                    if (!string.IsNullOrWhiteSpace(value))
                        ua.ParseAdd(value);
                    ua.ParseAdd(WikiClientUserAgent);
                    _ClientUserAgent = value;
                }
            }
        } 

        /// <summary>
        /// Referer.
        /// </summary>
        public string Referer { get; set; }

        /// <summary>
        /// Gets/Sets the cookies used in the requests.
        /// </summary>
        /// <remarks>
        /// <para>To persist user's login information, you can persist the value of this property.</para>
        /// <para>You can use the same CookieContainer with different <see cref="WikiClient"/>s.</para>
        /// </remarks>
        /// <exception cref="NotSupportedException">You have initialized this Client with a HttpMessageHandler that is not a HttpClientHandler.</exception>
        public CookieContainer CookieContainer
        {
            get { return _HttpClientHandler.CookieContainer; }
            set
            {
                if (_HttpClientHandler == null)
                    throw new NotSupportedException("Not supported when working with a HttpMessageHandler that is not a HttpClientHandler.");
                _HttpClientHandler.CookieContainer = value;
            }
        }

        internal HttpClient HttpClient { get; }

        #endregion

        private static readonly KeyValuePair<string, object>[] formatJsonKeyValue =
        {
            new KeyValuePair<string, object>("format", "json")
        };

        /// <inheritdoc />
        public override async Task<JToken> GetJsonAsync(string endPointUrl, WikiRequestMessage message, CancellationToken cancellationToken)
        {
            if (endPointUrl == null) throw new ArgumentNullException(nameof(endPointUrl));
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message is WikiFormRequestMessage form)
                message = new WikiFormRequestMessage(form.Id, form, formatJsonKeyValue, false);
            var result = await SendAsync(endPointUrl, message, cancellationToken);
            return result;
        }

        public WikiClient() : this(new HttpClientHandler(), true)
        {
            _HttpClientHandler.UseCookies = true;
            // https://www.mediawiki.org/wiki/API:Client_code
            // Please use GZip compression when making API calls (Accept-Encoding: gzip).
            // Bots eat up a lot of bandwidth, which is not free.
            if (_HttpClientHandler.SupportsAutomaticDecompression)
            {
                _HttpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
        }

        /// <param name="handler">The HttpMessageHandler responsible for processing the HTTP response messages.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        public WikiClient(HttpMessageHandler handler) : this(handler, true)
        {
        }

        /// <param name="handler">The HttpMessageHandler responsible for processing the HTTP response messages.</param>
        /// <param name="disposeHandler">Whether to automatically dispose the handler when disposing this Client.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        public WikiClient(HttpMessageHandler handler, bool disposeHandler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            HttpClient = new HttpClient(handler, disposeHandler);
            ClientUserAgent = null;
            _HttpClientHandler = handler as HttpClientHandler;
#if DEBUG
            HttpClient.DefaultRequestHeaders.Add("X-WCL-DEBUG-CLIENT-ID", GetHashCode().ToString());
#endif
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{GetType().Name}#{GetHashCode()}";
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HttpClient.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
