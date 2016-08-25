using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Generators
{
    public abstract class PageGeneratorBase
    {
        private int? _PagingSize;

        public PageGeneratorBase(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        public Site Site { get; }
        public WikiClient Client => Site.WikiClient;

        /// <summary>
        /// Maximum items returned per request.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per request.
        /// <c>null</c> if using the default limit.
        /// (5000 for bots and 500 for users.)
        /// </value>
        public int? PagingSize
        {
            get { return _PagingSize; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _PagingSize = value;
            }
        }

        /// <summary>
        /// Gets the actual value of <see cref="PagingSize"/> used for request.
        /// </summary>
        /// <value>
        /// The same of <see cref="PagingSize"/> if specified, or the default limit
        /// (5000 for bots and 500 for users) otherwise.
        /// </value>
        public int ActualPagingSize => PagingSize ?? (Site.UserInfo.HasRight(UserRights.ApiHighLimits) ? 5000 : 500);

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containning request value pairs.</returns>
        protected abstract IEnumerable<KeyValuePair<string, object>> GetGeneratorParams();

        /// <summary>
        /// Gets JSON result of the query operation with the specific generator.
        /// </summary>
        /// <returns>The root of JSON result. You may need to access query result by ["query"].</returns>
        internal IAsyncEnumerable<JObject> EnumJsonAsync(IEnumerable<KeyValuePair<string, string>> overridingParams)
        {
            var valuesDict = new Dictionary<string, object>
            {
                {"action", "query"},
                {"maxlag", 5}
            };
            foreach (var v in GetGeneratorParams())
                valuesDict[v.Key] = v.Value;
            foreach (var v in overridingParams)
                valuesDict[v.Key] = v.Value;
            Debug.Assert((string) valuesDict["action"] == "query");
            var eofReached = false;
            var resultCounter = 0;
            return new DelegateAsyncEnumerable<JObject>(async cancellation =>
            {
                if (eofReached) return null;
                cancellation.ThrowIfCancellationRequested();
                Site.Logger?.Trace(ToString() + ": Loading pages from #" + resultCounter);
                var jresult = await Client.GetJsonAsync(valuesDict);
                // continue.xxx
                // or query-continue.allpages.xxx
                var continuation = (JObject) (jresult["continue"]
                                              ?? ((JProperty) jresult["query-continue"]?.First)?.Value);
                if (continuation != null)
                {
                    // Prepare for the next page of list.
                    // Note for string of ISO date,
                    // (string) JToken == (string) (DateTime) JToken
                    // So we cannot use (string) p.Value or p.Value.ToString
                    foreach (var p in continuation.Properties())
                        valuesDict[p.Name] = p.Value.ToObject<object>();
                }
                else
                {
                    eofReached = true;
                }
                // If there's no result, "query" node will not exist.
                var jquery = (JObject) jresult["query"];
                if (jquery != null)
                    resultCounter += jquery.Count;
                else if (continuation != null)
                    Site.Logger?.Warn("Empty page list received.");
                cancellation.ThrowIfCancellationRequested();
                return Tuple.Create((JObject) jresult, true);
            });
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return GetType().Name;
        }
    }

    /// <summary>
    /// Represents a generator (or iterator) of <see cref="Page"/>.
    /// </summary>
    /// <typeparam name="T">The type of generated page instances.</typeparam>
    public abstract class PageGenerator<T> : PageGeneratorBase
        where T : Page
    {
        public PageGenerator(Site site) : base(site)
        {
        }

        /// <summary>
        /// Synchornously generate the sequence of pages.
        /// </summary>
        public IEnumerable<T> EnumPages()
        {
            return EnumPages(false);
        }

        /// <summary>
        /// Synchornously generate the sequence of pages.
        /// </summary>
        /// <param name="fetchContent">Whether to fetch the last revision and content of the page.</param>
        public IEnumerable<T> EnumPages(bool fetchContent)
        {
            return EnumPagesAsync(fetchContent).ToEnumerable();
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        public IAsyncEnumerable<T> EnumPagesAsync()
        {
            return EnumPagesAsync(false);
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        /// <param name="fetchContent">Whether to fetch the last revision and content of the page.</param>
        public IAsyncEnumerable<T> EnumPagesAsync(bool fetchContent)
        {
            return RequestManager.EnumPagesAsync<T>(this, fetchContent);
        }
    }
}
