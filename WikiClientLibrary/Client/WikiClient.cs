using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides basic operations for MediaWiki API.
    /// </summary>
    public partial class WikiClient
    {
        private int _MaxRetries;

        #region Configurations

        /// <summary>
        /// MediaWiki API endpoint.
        /// </summary>
        public string EndPointUrl { get; set; } = "https://test2.wikipedia.org/w/api.php";

        /// <summary>
        /// User Agent.
        /// </summary>
        public string UserAgent { get; set; } = "WikiClientLibrary/1.0 (.NET Portable; http://github.com/cxuesong)";

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

        public ILogger Logger { get; set; }
        #endregion

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        public async Task<JObject> GetJsonAsync(string queryParams)
        {
            if (queryParams == null) throw new ArgumentNullException(nameof(queryParams));
            var requestUrl = EndPointUrl + "?format=json&" + queryParams;
            var request = WebRequest.Create(requestUrl);
            InitializeRequest(request, "GET");
            var result = await SendAsync(request);
            return result;
        }

        public Task<JObject> GetJsonAsync(IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            return GetJsonAsync(Utility.EncodeValuePairs(queryParams));
        }

        public Task<JObject> GetJsonAsync(object queryParams)
        {
            return GetJsonAsync(Utility.EncodeValuePairs(queryParams));
        }

        private void InitializeRequest(WebRequest request, string method)
        {
            var hwr = (request as HttpWebRequest);
            if (hwr != null)
            {
                hwr.SetHeader("User-Agent", UserAgent);
                hwr.SetHeader("Referer", Referer);
            }
            request.Method = method;
            // The Timeout property affects only synchronous requests made with the GetResponse method.
            //request.Timeout = (int) Timeout.TotalMilliseconds;
        }

        public WikiClient()
        {
            
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
    }
}
