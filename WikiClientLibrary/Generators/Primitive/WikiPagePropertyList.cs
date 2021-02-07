using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{
    /// <summary>
    /// Represents a <c>list</c>-like sequence parsed from a property of MediaWiki page.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <remarks>
    /// Some properties of a MediaWiki page are inherently a list
    /// e.g. <c>link</c>(see <a href="https://www.mediawiki.org/wiki/API:Links">mw:API:Links</a>)
    /// that supports pagination, and that thus can be enumerated asynchronously.
    /// This class provides basic functionality for enumerating such list-like property values.
    /// </remarks>
    /// <seealso cref="Primitive.WikiPagePropertyGenerator{TItem}"/>
    /// <seealso cref="WikiList{T}"/>
    public abstract class WikiPagePropertyList<T> : IWikiList<T>
    {
        private int _PaginationSize = 10;

        /// <param name="site">The MediaWiki site this instance applies to.</param>
        protected WikiPagePropertyList(WikiSite site)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
        }

        /// <summary>Gets the MediaWiki site this instance applies to.</summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Gets/sets the page title from which to get the <c>list</c>-like property value.
        /// </summary>
        /// <remarks>If this property is not <c>null</c>, the value of this property will be used.
        /// Otherwise <see cref="PageId"/> will be effective.</remarks>
        public string? PageTitle { get; set; }

        /// <summary>
        /// Gets/sets the page ID from which to get the <c>list</c>-like property value.
        /// </summary>
        /// <remarks>If <see cref="PageTitle"/> is <c>null</c>, the value of this property will be used.
        /// Otherwise the other one will be effective.</remarks>
        public int PageId { get; set; }

        /// <summary>
        /// Gets/sets maximum items returned per MediaWiki API invocation.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per MediaWiki API invocation.
        /// This limit is 10 by default, and can be set as high as 500 for regular users,
        /// or 5000 for users with the <c>apihighlimits</c> right (typically bots and sysops). 
        /// </value>
        /// <remarks>
        /// This property decides how many items returned at most per MediaWiki API invocation.
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
        /// Gets the name of the property, used as the value of <c>prop</c> parameter in <c>action=query</c> request.
        /// </summary>
        public abstract string PropertyName { get; }

        /// <summary>
        /// When overridden, fills generator parameters for <c>action=query&amp;prop={PropertyName}</c> request.
        /// </summary>
        /// <returns>A sequence of fields, which will override the basic query parameters.</returns>
        public abstract IEnumerable<KeyValuePair<string, object?>> EnumListParameters();

        /// <summary>
        /// Parses an item contained in the <c>action=query&amp;prop={PropertyName}</c> JSON response.
        /// </summary>
        /// <param name="json">One of the item node under the JSON path <c>query.pages.{pageid}.{PropertyName}</c>.</param>
        /// <param name="jpage">The corresponding JSON node for the wiki page, under the JSON path <c>query.pages.{pageid}</c>.</param>
        /// <returns>The item that will be returned in the sequence from <see cref="EnumItemsAsync"/>.</returns>
        protected abstract T ItemFromJson(JToken json, JObject jpage);

        /// <inheritdoc />
        public IAsyncEnumerable<T> EnumItemsAsync(CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, object>
            {
                {"action", "query"},
                {"maxlag", 5},
                {"titles", PageTitle},
                {"prop", PropertyName}
            };
            if (PageTitle == null) queryParams.Add("pageids", PageId);
            foreach (var p in EnumListParameters()) queryParams.Add(p.Key, p.Value);
            return RequestHelper.QueryWithContinuation(Site, queryParams, () => Site.BeginActionScope(this), cancellationToken: cancellationToken)
                .SelectMany(jpages =>
                {
                    // If there's no result, "query" node will not exist.
                    if (jpages == null || jpages.Count == 0)
                        return AsyncEnumerable.Empty<T>();
                    return jpages.Values().SelectMany(jpage =>
                    {
                        var jprop = jpage[PropertyName];
                        // This can happen when there are multiple titles specified (such as stuffing multiple titles in PageTitle),
                        // and pagination is triggered.
                        // See https://github.com/CXuesong/WikiClientLibrary/issues/67.
                        if (jprop == null) return Enumerable.Empty<T>();
                        return jprop.Select(v => ItemFromJson(v, (JObject)jpage));
                    }).ToList().ToAsyncEnumerable();
                    // ToList is necessary. See
                    // https://github.com/CXuesong/WikiClientLibrary/issues/27
                });
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (PageTitle != null)
                return $"{GetType().Name}({PageTitle})";
            return $"{GetType().Name}(#{PageId})";
        }
    }
}