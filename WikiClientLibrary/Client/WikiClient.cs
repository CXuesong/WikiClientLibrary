using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides basic operations for MediaWiki API.
    /// </summary>
    public partial class WikiClient : IDisposable
    {

        #region Configurations

        private int _MaxRetries = 3;

        /// <summary>
        /// MediaWiki API endpoint.
        /// </summary>
        public string EndPointUrl { get; set; } = "https://test2.wikipedia.org/w/api.php";

        /// <summary>
        /// User Agent for client-side application.
        /// </summary>
        public string ClientUserAgent
        {
            get { return lastCustomUserAgent?.ToString(); }
            set
            {
                if (lastCustomUserAgent != null)
                    _HttpClient.DefaultRequestHeaders.UserAgent.Remove(lastCustomUserAgent);
                lastCustomUserAgent = ProductInfoHeaderValue.Parse(value);
                _HttpClient.DefaultRequestHeaders.UserAgent.Add(lastCustomUserAgent);
            }
        } 

        /// <summary>
        /// Referer.
        /// </summary>
        public string Referer { get; set; }

        /// <summary>
        /// Timeout for each query.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

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

        public CookieContainer CookieContainer
        {
            get { return _HttpClientHandler.CookieContainer; }
            set { _HttpClientHandler.CookieContainer = value; }
        }

        public ILogger Logger { get; set; }

        /// <summary>
        /// Time to wait before any modification operations.
        /// </summary>
        /// <remarks>Note this won't work as you may expected when you attemt to perform multi-threaded operations.</remarks>
        public TimeSpan ThrottleTime
        {
            get { return _ThrottleTime; }
            set
            {
                if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
                _ThrottleTime = value;
            }
        }

        #endregion

        private HttpClient _HttpClient;
        private HttpClientHandler _HttpClientHandler;
        private ProductInfoHeaderValue lastCustomUserAgent;
        private TimeSpan _ThrottleTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Returns a task which finishes after the time specified in <see cref="ThrottleTime"/> .
        /// </summary>
        internal Task WaitForThrottleAsync()
        {
            return Task.Delay(ThrottleTime);
        }

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        public async Task<JObject> GetJsonAsync(IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            if (queryParams == null) throw new ArgumentNullException(nameof(queryParams));
            var requestUrl = EndPointUrl + "?format=json&" + queryParams;
            var result = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, EndPointUrl)
            {
                Content = new FormUrlEncodedContent(new[] {new KeyValuePair<string, string>("format", "json")}
                    .Concat(queryParams)),
            });
            return result;
        }

        public Task<JObject> GetJsonAsync(object queryParams)
        {
            return GetJsonAsync(Utility.ToWikiStringValuePairs(queryParams));
        }

        public WikiClient()
        {
            _HttpClientHandler = new HttpClientHandler
            {
                UseCookies = true
            };
            _HttpClient = new HttpClient(_HttpClientHandler, true);
            _HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WikiClientLibrary/1.0 (.NET Portable; http://github.com/cxuesong/WikiClientLibrary)");
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{GetType()}@{EndPointUrl}";
        }

        public void Dispose()
        {
            _HttpClient.Dispose();
        }
    }
}
