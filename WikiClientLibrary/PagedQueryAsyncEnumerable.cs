using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary
{
    /// <summary>
    /// Asynchronously enumerate paged query results. This AsyncEnumerable enumerates
    /// a sequence of <see cref="JObject"/>, corresponds to ROOT.query node of fetched JSON.
    /// </summary>
    internal class PagedQueryAsyncEnumerable : IAsyncEnumerable<JObject>
    {
        private readonly Site _Site;
        private readonly IDictionary<string, object> _Parameters;

        public PagedQueryAsyncEnumerable(Site site, IDictionary<string, object> parameters)
        {
            _Site = site;
            _Parameters = parameters;
            Debug.Assert((string) _Parameters["action"] == "query");
        }

        /// <summary>
        /// Gets an asynchronous enumerator over the sequence.
        /// </summary>
        /// <returns>
        /// Enumerator for asynchronous enumeration over the sequence.
        /// </returns>
        public IAsyncEnumerator<JObject> GetEnumerator()
        {
            var eofReached = false;
            var pa = new Dictionary<string, object>(_Parameters);
            var ienu = new DelegateAsyncEnumerable<JObject>(async cancellation =>
            {
                BEGIN:
                if (eofReached) return null;
                cancellation.ThrowIfCancellationRequested();
                var jresult = await _Site.WikiClient.GetJsonAsync(pa);
                // continue.xxx
                // or query-continue.allpages.xxx
                var continuation = (JObject) (jresult["continue"]
                                              ?? ((JProperty) jresult["query-continue"]?.First)?.Value);
                if (continuation != null)
                {
                    // Prepare for the next page of list.
                    foreach (var p in continuation.Properties())
                        pa[p.Name] = p.Value.ToObject<object>();
                }
                else
                {
                    eofReached = true;
                }
                // If there's no result, "query" node will not exist.
                var queryNode = (JObject) jresult["query"];
                if (queryNode != null)
                    return Tuple.Create(queryNode, true);
                // If so, let's see if there're more results.
                if (continuation != null)
                    _Site.Logger?.Warn("Empty page list received.");
                goto BEGIN;
            });
            return ienu.GetEnumerator();
        }
    }
}
