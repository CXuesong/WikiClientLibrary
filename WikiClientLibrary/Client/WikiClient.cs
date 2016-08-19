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
        private int _MaxRetries;

        #region Configurations

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

        #endregion

        private HttpClient _HttpClient;
        private HttpClientHandler _HttpClientHandler;
        private ProductInfoHeaderValue lastCustomUserAgent;

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        public async Task<JObject> GetJsonAsync(IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            if (queryParams == null) throw new ArgumentNullException(nameof(queryParams));
            var requestUrl = EndPointUrl + "?format=json&" + queryParams;
            var request = new HttpRequestMessage(HttpMethod.Post, EndPointUrl)
            {
                Content = new FormUrlEncodedContent(new[] {new KeyValuePair<string, string>("format", "json")}
                    .Concat(queryParams)),
            };
            var result = await SendAsync(request);
            return result;
        }

        public Task<JObject> GetJsonAsync(object queryParams)
        {
            return GetJsonAsync(Utility.ToStringValuePairs(queryParams));
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
            return $"{this.GetType()}@{EndPointUrl}";
        }

        public void Dispose()
        {
            _HttpClient.Dispose();
        }
    }
}
