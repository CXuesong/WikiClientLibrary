using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading;

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
        public const string WikiClientUserAgent = "WikiClientLibrary/0.5 (.NET Portable; http://github.com/cxuesong/WikiClientLibrary)";

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

        /// <inheritdoc />
        public override async Task<JToken> GetJsonAsync(string endPointUrl, IEnumerable<KeyValuePair<string, string>> queryParams,
            CancellationToken cancellationToken)
        {
            if (queryParams == null) throw new ArgumentNullException(nameof(queryParams));
            var result = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, endPointUrl)
            {
                Content = new FormLongUrlEncodedContent(new[] {new KeyValuePair<string, string>("format", "json")}
                    .Concat(queryParams)),
            }, true, cancellationToken);
            return result;
        }

        /// <inheritdoc />
        public override async Task<JToken> GetJsonAsync(string endPointUrl, HttpContent postContent, CancellationToken cancellationToken)
        {
            if (postContent == null) throw new ArgumentNullException(nameof(postContent));
            // Implies we want JSON result.
            var result = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, endPointUrl)
            {
                Content = postContent,
            }, false, cancellationToken);
            // No, we don't retry.
            // HttpContent will usually be disposed after a request.
            // We cannot ask for a HttpContent factory becuase in this case,
            // caller may have Stream to pass in, which cannot be rebuilt.
            return result;
        }

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        public override Task<JToken> GetJsonAsync(string endPointUrl, object queryParams, CancellationToken cancellationToken)
        {
            return GetJsonAsync(endPointUrl, Utility.ToWikiStringValuePairs(queryParams), cancellationToken);
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
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{GetType()}";
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
