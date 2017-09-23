using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents a generator (or iterator) of <see cref="WikiPage"/>.
    /// Generator implementations should use its generic version, <see cref="WikiPageGenerator{T}"/>, as base class.
    /// </summary>
    public abstract class WikiPageGeneratorBase : IWikiClientLoggable
    {

        internal ILogger logger = NullLogger.Instance;
        private int? _PagingSize;

        internal WikiPageGeneratorBase(WikiSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        public WikiSite Site { get; }

        /// <summary>
        /// Maximum items requested per MediaWiki API invocation.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per MediaWiki API invocation.
        /// <c>null</c> if using the default limit.
        /// (5000 for bots and 500 for users for normal requests,
        /// and 1/10 of the value when requesting for page content.)
        /// </value>
        /// <remarks>
        /// This property decides how many items returned at most per MediaWiki API invocation.
        /// Note that the returned enumerator of <see cref="WikiPageGenerator{T}.EnumPagesAsync()"/>
        /// will automatically make MediaWiki API invocation to ask for the next batch of results,
        /// when needed.
        /// </remarks>
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
        /// Gets the actual value of <see cref="PagingSize"/> used for request,
        /// assuming the page content is not needed when enumerating pages.
        /// </summary>
        /// <returns>
        /// The same of <see cref="PagingSize"/> if specified, or the default limit
        /// (5000 for bots and 500 for users) otherwise.
        /// </returns>
        public int GetActualPagingSize()
        {
            return GetActualPagingSize(PageQueryOptions.None);
        }

        /// <summary>
        /// Gets the actual value of <see cref="PagingSize"/> used for request.
        /// </summary>
        /// <param name="options">The options used when attempting to enumerate the pages.</param>
        /// <returns>
        /// The estimated items (i.e. wiki pages) count per page.
        /// If <see cref="PagingSize"/> is set, then the returned value will be identical to it.
        /// </returns>
        /// <remarks>
        /// If <see cref="PagingSize"/> is <c>null</c>, and <see cref="PageQueryOptions.FetchContent"/> is specified,
        /// the default limit will be 1/10 of the original default limit (500 for bots and 50 for users).
        /// (See https://www.mediawiki.org/wiki/API:Revisions .)
        /// If you have manually set <see cref="PagingSize"/>, this function will directly return the value you have set,
        /// but any value exceeding the server limit will case problems, such as empty content retrieved (even if
        /// you have set <see cref="PageQueryOptions.FetchContent"/>), or <see cref="WikiClientException"/>.
        /// </remarks>
        public int GetActualPagingSize(PageQueryOptions options)
        {
            if (PagingSize != null) return PagingSize.Value;
            return (options & PageQueryOptions.FetchContent) == PageQueryOptions.FetchContent
                ? Site.ListingPagingSize/10
                : Site.ListingPagingSize;
        }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <param name="actualPagingSize"></param>
        /// <returns>The dictioanry containing request value pairs, which will be overrided by the basic query parameters.</returns>
        protected abstract IEnumerable<KeyValuePair<string, object>> GetGeneratorParams(int actualPagingSize);

        /// <summary>
        /// Determins whether to remove duplicate page results generated from generator results.
        /// </summary>
        protected virtual bool DistinctGeneratedPages => false;

        /// <summary>
        /// Gets JSON result of the query operation with the specific generator.
        /// </summary>
        /// <returns>The root of JSON result. You may need to access query result by ["query"].</returns>
        internal IAsyncEnumerable<JObject> EnumJsonAsync(IEnumerable<KeyValuePair<string, object>> overridingParams, int actualPagingSize)
        {
            var valuesDict = new Dictionary<string, object>
            {
                {"action", "query"},
                {"maxlag", 5}
            };
            foreach (var v in GetGeneratorParams(actualPagingSize))
                valuesDict[v.Key] = v.Value;
            foreach (var v in overridingParams)
                valuesDict[v.Key] = v.Value;
            Debug.Assert((string) valuesDict["action"] == "query");
            return new PagedQueryAsyncEnumerable(Site, valuesDict, DistinctGeneratedPages);
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

        public void SetLoggerFactory(ILoggerFactory factory)
        {
            logger = factory == null ? (ILogger)NullLogger.Instance : factory.CreateLogger(GetType());
        }
    }

    /// <summary>
    /// Represents a generator (or iterator) of <see cref="WikiPage"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of generated page instances.
    /// The actual runtime subtype will be automatically inferred.
    /// </typeparam>
    public abstract class WikiPageGenerator<T> : WikiPageGeneratorBase
        where T : WikiPage
    {
        public WikiPageGenerator(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// Synchornously generate the sequence of pages.
        /// </summary>
        public IEnumerable<T> EnumPages()
        {
            return EnumPages(PageQueryOptions.None);
        }

        /// <summary>
        /// Synchornously generate the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        public IEnumerable<T> EnumPages(PageQueryOptions options)
        {
            return EnumPagesAsync(options).ToEnumerable();
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        public IAsyncEnumerable<T> EnumPagesAsync()
        {
            return EnumPagesAsync(PageQueryOptions.None);
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        public IAsyncEnumerable<T> EnumPagesAsync(PageQueryOptions options)
        {
            var actualPagingSize = GetActualPagingSize(options);
            return RequestHelper.EnumPagesAsync(this, options, actualPagingSize).Cast<T>();
        }
    }
}
