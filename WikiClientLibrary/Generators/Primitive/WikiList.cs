using System;
using System.Collections.Generic;
using System.Linq;
using AsyncEnumerableExtensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{

    /// <summary>
    /// Provides method for asynchronously generating a sequence of items.
    /// </summary>
    /// <typeparam name="T">The page instance type.</typeparam>
    public interface IWikiList<out T>
    {

        /// <summary>
        /// Asynchronously enumerates all the items in the list.
        /// </summary>
        /// <remarks>In most cases, the whole sequence will be very long. To take only the top <c>n</c> results
        /// from the sequence, chain the returned <see cref="IAsyncEnumerable{T}"/> with <see cref="AsyncEnumerable.Take{TSource}"/>
        /// extension method.</remarks>
        IAsyncEnumerable<T> EnumItemsAsync();

    }

    /// <summary>
    /// Represents a configured MediaWiki <c>list</c>. (<a href="https://www.mediawiki.org/wiki/API:Lists">mw:API:Lists</a>)
    /// </summary>
    /// <typeparam name="T">The type of listed items.</typeparam>
    /// <seealso cref="WikiPageGenerator{TItem}"/>
    /// <seealso cref="WikiPagePropertyList{T}"/>
    public abstract class WikiList<T> : IWikiList<T>
    {

        private int _PaginationSize = 10;

        /// <param name="site">The MediaWiki site this instance applies to.</param>
        public WikiList(WikiSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        /// <summary>Gets the MediaWiki site this instance applies to.</summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Gets/sets maximum items returned per MediaWiki API invocation.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per MediaWiki API invocation.
        /// This limit is 10 by default, and can be set as high as 500 for regular users,
        /// or 5000 for users with the <c>apihighlimits</c> right (typically in bot or sysop group).
        /// </value>
        /// <remarks>
        /// This property decides how many items returned at most per MediaWiki API invocation.
        /// Note that the enumerator returned from <see cref="EnumItemsAsync"/>
        /// or <seealso cref="WikiPageGenerator{TItem,TPage}.EnumPagesAsync()"/>
        /// will automatically make further MediaWiki API invocations to ask for the next batch of results,
        /// when needed.
        /// </remarks>
        public int PaginationSize
        {
            get { return _PaginationSize; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _PaginationSize = value;
            }
        }

        /// <summary>
        /// The name of list, used as the value of <c>list</c> parameter in <c>action=query</c> request.
        /// </summary>
        public abstract string ListName { get; }

        /// <summary>
        /// When overridden, fills generator parameters for <c>action=query&amp;list={ListName}</c> request.
        /// </summary>
        /// <returns>A sequence of fields, which will override the basic query parameters.</returns>
        public abstract IEnumerable<KeyValuePair<string, object>> EnumListParameters();

        /// <summary>
        /// Parses an item contained in the <c>action=query&amp;list=</c> JSON response.
        /// </summary>
        /// <param name="json">One of the item node under the JSON path <c>query/{listname}</c>.</param>
        /// <returns>The item that will be returned in the sequence from <see cref="EnumItemsAsync"/>.</returns>
        protected abstract T ItemFromJson(JToken json);

        /// <inheritdoc />
        public IAsyncEnumerable<T> EnumItemsAsync()
        {
            return AsyncEnumerableFactory.FromAsyncGenerator<T>(async (sink, ct) =>
            {
                var queryParams = new Dictionary<string, object>
                {
                    {"action", "query"},
                    {"maxlag", 5},
                    {"list", ListName},
                };
                foreach (var p in EnumListParameters()) queryParams.Add(p.Key, p.Value);
                ct.ThrowIfCancellationRequested();
                using (Site.BeginActionScope(this))
                {
                    NEXT_PAGE:
                    var jresult = await Site.GetJsonAsync(new MediaWikiFormRequestMessage(queryParams), ct);
                    // If there's no result, "query" node will not exist.
                    var queryNode = (JObject)jresult["query"];
                    if (queryNode == null || queryNode.Count == 0) goto END_OF_PARSING;
                    var listNode = queryNode[ListName];
                    if (listNode == null)
                    {
                        if (queryNode.Count > 1)
                            throw new UnexpectedDataException("Cannot detect the JSON node containing list result.");
                        listNode = ((JProperty)queryNode.First).Value;
                    }
                    await sink.YieldAndWait(listNode.Select(ItemFromJson));
                    END_OF_PARSING:
                    var continuation = (JObject)(jresult["continue"]
                                                 ?? ((JProperty)jresult["query-continue"]?.First)?.Value);
                    // No more results.
                    if (continuation == null || continuation.Count == 0) return;
                    // Check for continuation.
                    foreach (var p in continuation.Properties())
                    {
                        object parsed;
                        if (p.Value is JValue value) parsed = value.Value;
                        else parsed = p.Value.ToString(Formatting.None);
                        queryParams[p.Name] = parsed;
                    }
                    if (queryNode == null || queryNode.Count == 0)
                        Site.Logger.LogWarning("Empty query page with continuation received.");
                    goto NEXT_PAGE;
                }
            });

        }
    }

}
