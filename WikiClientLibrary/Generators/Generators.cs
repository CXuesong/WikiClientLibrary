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
    /// <summary>
    /// Represents a generator (or iterator) of <see cref="Page"/>.
    /// </summary>
    public abstract class PageGenerator
    {
        public Site Site { get; }

        public WikiClient Client => Site.WikiClient;

        public PageGenerator(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containning request value pairs.</returns>
        protected abstract IEnumerable<KeyValuePair<string, string>> GetGeneratorParams();

        /// <summary>
        /// Gets JSON result of the query operation with the specific generator.
        /// </summary>
        /// <returns>The root of JSON result. You may need to access query result by ["query"].</returns>
        internal IAsyncEnumerable<JObject> EnumJsonAsync(IEnumerable<KeyValuePair<string, string>> overridingParams)
        {
            var valuesDict = new Dictionary<string, string>
            {
                {"action", "query"},
                {"maxlag", "5"}
            };
            foreach (var v in GetGeneratorParams())
                valuesDict[v.Key] = v.Value;
            foreach (var v in overridingParams)
                valuesDict[v.Key] = v.Value;
            Debug.Assert(valuesDict["action"] == "query");
            var eofReached = false;
            var resultCounter = 0;
            return new DelegateAsyncEnumerable<JObject>(async cancellation =>
            {
                if (eofReached) return null;
                cancellation.ThrowIfCancellationRequested();
                Site.Logger?.Trace(ToString() + ": Loading pages from #" + resultCounter);
                var jresult = await Client.GetJsonAsync(valuesDict);
                var continuation = (JObject)(jresult["continue"] ?? jresult["query-continue"]);
                if (continuation != null)
                {
                    // Prepare for the next page of list.
                    foreach (var p in continuation.Properties())
                        valuesDict[p.Name] = (string) p.Value;
                }
                else
                {
                    eofReached = true;
                }
                resultCounter += ((JObject) jresult["query"]).Count;
                cancellation.ThrowIfCancellationRequested();
                return Tuple.Create((JObject) jresult, true);
            });
        }

        /// <summary>
        /// Synchornously generate the sequence of pages.
        /// </summary>
        public IEnumerable<Page> EnumPages()
        {
            return EnumPages(false);
        }

        /// <summary>
        /// Synchornously generate the sequence of pages.
        /// </summary>
        /// <param name="fetchContent">Whether to fetch the last revision and content of the page.</param>
        public IEnumerable<Page> EnumPages(bool fetchContent)
        {
            return EnumPagesAsync(fetchContent).ToEnumerable();
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        public IAsyncEnumerable<Page> EnumPagesAsync()
        {
            return EnumPagesAsync(false);
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        /// <param name="fetchContent">Whether to fetch the last revision and content of the page.</param>
        public IAsyncEnumerable<Page> EnumPagesAsync(bool fetchContent)
        {
            return QueryManager.EnumPagesAsync(this, fetchContent);
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
}
