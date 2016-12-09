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
    /// Generator implementations should use its generic version, <see cref="PageGenerator{T}"/>, as base class.
    /// </summary>
    public abstract class PageGeneratorBase
    {
        private int? _PagingSize;

        internal PageGeneratorBase(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        public Site Site { get; }

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
        public int ActualPagingSize => PagingSize ?? Site.ListingPagingSize;

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containing request value pairs, which will be overrided by the basic query parameters.</returns>
        protected abstract IEnumerable<KeyValuePair<string, object>> GetGeneratorParams();

        /// <summary>
        /// Determins whether to remove duplicate page results generated from generator results.
        /// </summary>
        protected virtual bool DistinctGeneratedPages => false;

        /// <summary>
        /// Gets JSON result of the query operation with the specific generator.
        /// </summary>
        /// <returns>The root of JSON result. You may need to access query result by ["query"].</returns>
        internal IAsyncEnumerable<JObject> EnumJsonAsync(IEnumerable<KeyValuePair<string, object>> overridingParams)
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
            return RequestHelper.EnumPagesAsync(this, options).Cast<T>();
        }
    }
}
