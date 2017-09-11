using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// Asynchronously enumerate paged query results. This AsyncEnumerable enumerates
    /// a sequence of <see cref="JObject"/>, corresponds to ROOT.query node of fetched JSON.
    /// </summary>
    internal class PagedQueryAsyncEnumerable : IAsyncEnumerable<JObject>
    {
        private readonly WikiSite _Site;
        private readonly IDictionary<string, object> _Parameters;
        private readonly bool _DistinctPages;

        public PagedQueryAsyncEnumerable(WikiSite site, IDictionary<string, object> parameters) : this(site, parameters, false)
        {
        }

        // distinctPages: Used by RecentChangesGenerator, removes duplicate page results generated from generator results.
        public PagedQueryAsyncEnumerable(WikiSite site, IDictionary<string, object> parameters, bool distinctPages)
        {
            _Site = site;
            _Parameters = parameters;
            _DistinctPages = distinctPages;
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
            var retrivedPageIds = _DistinctPages ? new HashSet<int>() : null;
            var ienu = new DelegateAsyncEnumerable<JObject>(async cancellation =>
            {
                BEGIN:
                if (eofReached) return null;
                cancellation.ThrowIfCancellationRequested();
                var jresult = await _Site.PostValuesAsync(pa, cancellation);
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
                {
                    var pages = (JObject) queryNode["pages"];
                    if (retrivedPageIds != null && pages != null)
                    {
                        // Remove duplicate results
                        var duplicateKeys = new List<string>(pages.Count);
                        foreach (var jpage in pages)
                        {
                            if (!retrivedPageIds.Add(Convert.ToInt32(jpage.Key)))
                            {
                                // The page has been retrieved before.
                                duplicateKeys.Add(jpage.Key);
                            }
                        }
                        var originalPageCount = pages;
                        foreach (var k in duplicateKeys) pages.Remove(k);
                        _Site.logger.LogWarning("Received {Count} results on {Site}, {DistinctCount} distinct results.",
                            originalPageCount, _Site, pages.Count);
                    }
                    return Tuple.Create(queryNode, true);
                }
                // If so, let's see if there're more results.
                if (continuation != null)
                    _Site.logger.LogWarning("Empty query page with continuation received on {Site}.", _Site);
                goto BEGIN;
            });
            return ienu.GetEnumerator();
        }
    }
}
