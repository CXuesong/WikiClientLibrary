using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides basic operations for MediaWiki API via HTTP(S).
    /// </summary>
    public class WikiClient : IWikiClient, IWikiClientLoggable, IDisposable
    {

#if NET5_0
        private const string targetFramework = ".NET 5.0";
#elif NETSTANDARD2_1
        private const string targetFramework = ".NET Standard 2.1";
#endif

        /// <summary>
        /// The User Agent of Wiki Client Library.
        /// </summary>
        public const string WikiClientUserAgent = "WikiClientLibrary/0.7 (" + targetFramework + "; http://github.com/cxuesong/WikiClientLibrary)";

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

        #region Configurations

        private string _ClientUserAgent;
        private readonly HttpClientHandler _HttpClientHandler;
        private int _MaxRetries = 3;
        private ILogger _Logger = NullLogger.Instance;

        /// <summary>
        /// Timeout for each query.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Delay before each retry.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Max retries count.
        /// </summary>
        public int MaxRetries
        {
            get { return _MaxRetries; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _MaxRetries = value;
            }
        }

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
                    throw new NotSupportedException(Prompts.ExceptionWikiClientNonHttpClientHandler);
                _HttpClientHandler.CookieContainer = value;
            }
        }

        internal HttpClient HttpClient { get; }

        /// <inheritdoc />
        public ILogger Logger
        {
            get => _Logger;
            set => _Logger = value ?? NullLogger.Instance;
        }

        #endregion

        /// <inheritdoc />
        public async Task<T> InvokeAsync<T>(string endPointUrl, WikiRequestMessage message,
            IWikiResponseMessageParser<T> responseParser, CancellationToken cancellationToken)
        {
            if (endPointUrl == null) throw new ArgumentNullException(nameof(endPointUrl));
            if (message == null) throw new ArgumentNullException(nameof(message));
            using (this.BeginActionScope(null, message))
            {
                var result = await SendAsync(endPointUrl, message, responseParser, cancellationToken);
                return result;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{GetType().Name}#{GetHashCode()}";
        }

        /// <summary>
        /// Creates an HTTP request message with the given endpoint URL and <see cref="WikiRequestMessage"/> instance.
        /// </summary>
        /// <param name="endpointUrl">MediaWiki API endpoint URL.</param>
        /// <param name="message">The MediaWiki API request message to be sent.</param>
        /// <returns>The actual <see cref="HttpRequestMessage"/> to be sent.</returns>
        /// <remarks>
        /// When overriding this method in derived class, you may change the message headers and/or content after
        /// getting the <see cref="HttpRequestMessage"/> instance from base implementation,
        /// before returning the HTTP request message.
        /// </remarks>
        protected virtual HttpRequestMessage CreateHttpRequestMessage(string endpointUrl, WikiRequestMessage message)
        {
            var url = endpointUrl;
            var query = message.GetHttpQuery();
            if (query != null) url = url + "?" + query;
            return new HttpRequestMessage(message.GetHttpMethod(), url) { Content = message.GetHttpContent() };
        }

        private async Task<T> SendAsync<T>(string endPointUrl, WikiRequestMessage message,
            IWikiResponseMessageParser<T> responseParser, CancellationToken cancellationToken)
        {
            Debug.Assert(endPointUrl != null);
            Debug.Assert(message != null);

            var httpRequest = CreateHttpRequestMessage(endPointUrl, message);
            var retries = 0;

            async Task<bool> PrepareForRetry(TimeSpan delay)
            {
                if (retries >= MaxRetries) return false;
                retries++;
                try
                {
                    httpRequest = CreateHttpRequestMessage(endPointUrl, message);
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    // Some content (e.g. StreamContent with un-seekable Stream) may throw this exception
                    // on the second try.
                    Logger.LogWarning("Cannot retry: {Exception}.", ex.Message);
                    return false;
                }
                Logger.LogDebug("Retry #{Retries} after {Delay}.", retries, RetryDelay);
                await Task.Delay(delay, cancellationToken);
                return true;
            }

        RETRY:
            Logger.LogTrace("Initiate request to: {EndPointUrl}.", endPointUrl);
            cancellationToken.ThrowIfCancellationRequested();
            var requestSw = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                // Use await instead of responseTask.Result to unwrap Exceptions.
                // Or AggregateException might be thrown.
                using (var responseCancellation = new CancellationTokenSource(Timeout))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, responseCancellation.Token))
                    response = await HttpClient.SendAsync(httpRequest, linkedCts.Token);
                // The request has been finished.
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogWarning("Cancelled via CancellationToken.");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                Logger.LogWarning("Timeout.");
                if (!await PrepareForRetry(RetryDelay)) throw new TimeoutException();
                goto RETRY;
            }
            catch (HttpRequestException ex)
            {
#if BCL_FEATURE_WEB_EXCEPTION
                if (ex.InnerException is WebException ex1 && ex1.Status == WebExceptionStatus.SecureChannelFailure)
                {
#else
                // .NET 4.5
                var ex1 = ex.InnerException;
                if (ex1 != null && ex1.GetType().FullName == "System.Net.WebException")
                {
                    var statusProperty = ex1.GetType().GetRuntimeProperty("Status");
                    if (statusProperty != null && statusProperty.GetValue(ex1)?.ToString() == "SecureChannelFailure")
#endif
                    {
                        throw new HttpRequestException(ex1.Message + Prompts.ExceptionSecureChannelFailureHint, ex)
                        {
                            HelpLink = MediaWikiHelper.ExceptionTroubleshootingHelpLink
                        };
                    }
                }
                throw;
            }
            using (response)
            {
                // Validate response.
                var statusCode = (int)response.StatusCode;
                Logger.LogTrace("HTTP {StatusCode}, elapsed: {Time}", statusCode, requestSw.Elapsed);
                if (!response.IsSuccessStatusCode)
                    Logger.LogWarning("HTTP {StatusCode} {Reason}.", statusCode, response.ReasonPhrase, requestSw.Elapsed);
                var localRetryDelay = RetryDelay;
                if (response.Headers.RetryAfter != null)
                {
                    Logger.LogWarning("Detected Retry-After header in HTTP response: {RetryAfter}.", response.Headers.RetryAfter);
                    if (retries < MaxRetries)
                    {
                        // Service Error. We can retry.
                        // HTTP 503 or 200 : https://www.mediawiki.org/wiki/Manual:Maxlag_parameter
                        // Delay per Retry-After Header
                        var date = response.Headers.RetryAfter.Date;
                        var delay = response.Headers.RetryAfter.Delta;
                        if (delay == null && date != null) delay = date - DateTimeOffset.Now;
                        // Or use the default delay
                        if (delay != null && delay < RetryDelay)
                            localRetryDelay = delay.Value;
                    }
                }
                // It's responseParser's turn to check status code.
                cancellationToken.ThrowIfCancellationRequested();
                var context = new WikiResponseParsingContext(Logger, cancellationToken);
                try
                {
                    var parsed = await responseParser.ParseResponseAsync(response, context);
                    if (context.NeedRetry)
                    {
                        if (await PrepareForRetry(localRetryDelay)) goto RETRY;
                        throw new InvalidOperationException(Prompts.ExceptionWikiClientReachedMaxRetries);
                    }
                    return (T)parsed;
                }
                catch (Exception ex)
                {
                    if (context.NeedRetry && await PrepareForRetry(localRetryDelay))
                    {
                        Logger.LogWarning("{Parser}: {Message}", responseParser, ex.Message);
                        goto RETRY;
                    }
                    Logger.LogWarning(new EventId(), ex, "Parser {Parser} throws an Exception.", responseParser);
                    throw;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                HttpClient.Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
