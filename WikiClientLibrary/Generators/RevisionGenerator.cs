using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents a generator (or iterator) of <see cref="Revision"/> on a specific page.
    /// </summary>
    public class RevisionGenerator
    {
        private int? _PagingSize;

        public RevisionGenerator(Page page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            Debug.Assert(page.Site != null);
            Page = page;
            Site = page.Site;
        }

        public WikiSite Site { get; }

        /// <summary>
        /// Maximum items returned per request.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per request.
        /// <c>null</c> if using the default limit.
        /// (5000 for bots and 500 for users.)
        /// </value>
        /// <remarks>
        /// If you're also requesting for the content of each page,
        /// you might need to reduce this value to somewhere below 50,
        /// or the content might be <c>null</c> for some retrieved pages.
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
        /// Gets the actual value of <see cref="PagingSize"/> used for request.
        /// </summary>
        /// <value>
        /// The same of <see cref="PagingSize"/> if specified, or the default limit
        /// (5000 for bots and 500 for users) otherwise.
        /// </value>
        public int ActualPagingSize => PagingSize ?? Site.ListingPagingSize;

        /// <summary>
        /// The page to query for revisions.
        /// </summary>
        public Page Page { get; }

        /// <summary>
        /// Whether to list revisions in an ascending order of time.
        /// </summary>
        /// <value><c>true</c>, if oldest revisions are listed first; or <c>false</c>, if newest revisions are listed first.</value>
        /// <remarks>
        /// Any specified <see cref="StartTime"/> value must be later than any specified <see cref="EndTime"/> value.
        /// This requirement is reversed if <see cref="TimeAscending"/> is <c>true</c>.
        /// </remarks>
        public bool TimeAscending { get; set; } = false;

        /// <summary>
        /// The timestamp to start listing from.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The timestamp to end listing at.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Revision ID to start listing from.
        /// </summary>
        public int? StartRevisionId { get; set; }

        /// <summary>
        /// Revision ID to stop listing at. 
        /// </summary>
        public int? EndRevisionId { get; set; }

        /// <summary>
        /// Only list revisions made by this user.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Do not list revisions made by this user.
        /// </summary>
        public string ExcludedUserName { get; set; }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containing request value pairs, which will override the basic query parameters.</returns>
        internal IEnumerable<KeyValuePair<string, object>> GetGeneratorParams()
        {
            var pa = new Dictionary<string, object>
            {
                {"rvlimit", ActualPagingSize},
                {"rvdir", TimeAscending ? "newer" : "older"},
                {"titles", Page.Title},
                {"rvstart", StartTime},
                {"rvend", EndTime},
                {"rvstartid", StartRevisionId},
                {"rvendid", EndRevisionId},
                {"rvuser", UserName},
                {"rvexcludeuser", ExcludedUserName},
            };
            return pa;
        }

        /// <summary>
        /// Asynchornously generate the sequence of revisions.
        /// </summary>
        public IAsyncEnumerable<Revision> EnumRevisionsAsync()
        {
            return EnumRevisionsAsync(PageQueryOptions.None);
        }

        /// <summary>
        /// Asynchornously generate the sequence of revisions.
        /// </summary>
        /// <param name="options">Options when querying for the revisions. Note <see cref="PageQueryOptions.ResolveRedirects"/> will raise exception.</param>
        /// <exception cref="ArgumentException"><see cref="PageQueryOptions.ResolveRedirects"/> is set.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Page"/> is <c>null</c>.</exception>
        public IAsyncEnumerable<Revision> EnumRevisionsAsync(PageQueryOptions options)
        {
            // We do not resolve redirects.
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when querying for revisions.", nameof(options));
            return RequestHelper.EnumRevisionsAsync(this, options);
        }
    }
}
