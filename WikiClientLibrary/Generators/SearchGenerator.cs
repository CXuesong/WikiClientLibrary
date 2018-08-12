using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Search titles and text.
    /// (<a href="https://www.mediawiki.org/wiki/API:Search">mw:API:Search</a>, MediaWiki 1.11+)
    /// </summary>
    /// <remarks>
    /// <para>For full-text search on Wikia, use <c>LocalWikiSearchList</c> in <c>WikiClientLibrary.Wikia.WikiaApi</c> namespace.</para>
    /// </remarks>
    public class SearchGenerator : WikiPageGenerator<SearchResultItem>
    {
        private SearchableField _MatchingField = SearchableField.Text;

        private static readonly IList<int> defaultNamespace = new ReadOnlyCollection<int>(new[] {0});

        /// <inheritdoc />
        public SearchGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        /// <param name="keyword">Search for all page titles (or content) that have this value.</param>
        public SearchGenerator(WikiSite site, string keyword) : base(site)
        {
            Keyword = keyword;
        }

        /// <summary>
        /// Search for all page titles (or content) that have this value.
        /// </summary>
        public string Keyword { get; set; }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>The namespace(s) to enumerate. No more than 50 (500 for bots) allowed.
        /// See <see cref="BuiltInNamespaces"/> for a list of MediaWiki built-in namespace IDs.
        /// Set to <c>null</c> to search in all the namespaces. (Default: [0], i.e. Main Namespace)</value>
        public IEnumerable<int> NamespaceIds { get; set; } = defaultNamespace;

        /// <summary>
        /// Search inside the text or titles.
        /// </summary>
        public SearchableField MatchingField
        {
            get { return _MatchingField; }
            set
            {
                if (!Enum.IsDefined(typeof(SearchableField), value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                _MatchingField = value;
            }
        }

        /// <summary>
        /// Include interwiki results in the search, if available. (Default: false, MediaWiki 1.23+)
        /// </summary>
        public bool IncludesInterwiki { get; set; }

        /// <summary>
        /// Class name of search back-end to use (Default: $wgSearchType, MediaWiki 1.22+)
        /// </summary>
        public string BackendName { get; set; }

        /// <inheritdoc />
        public override string ListName => "search";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            var dict = new Dictionary<string, object>
            {
                {"srsearch", Keyword},
                {"srnamespace", NamespaceIds == null ? "*" : MediaWikiHelper.JoinValues(NamespaceIds)},
                {"srwhat", MatchingField},
                {"srlimit", PaginationSize},
                {"srinterwiki", IncludesInterwiki},
                {"srbackend", BackendName}
            };
            // Include redirect pages in the search. From 1.23 onwards, redirects are always included. (Removed in 1.23)
            if (Site.SiteInfo.Version < new Version(1, 23))
                dict["srredirects"] = true;
            switch (MatchingField)
            {
                case SearchableField.Title:
                    dict["srwhat"] = "title";
                    break;
                case SearchableField.Text:
                    dict["srwhat"] = "text";
                    break;
                case SearchableField.NearMatch:
                    dict["srwhat"] = "nearmatch";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return dict;
        }

        /// <inheritdoc />
        protected override SearchResultItem ItemFromJson(JToken json)
        {
            return json.ToObject<SearchResultItem>(Utility.WikiJsonSerializer);
        }
    }

    /// <summary>
    /// Used in <see cref="SearchGenerator.MatchingField"/>.
    /// </summary>
    public enum SearchableField
    {
        /// <summary>
        /// Use the site MediaWiki site default behavior.
        /// </summary>
        Default,
        /// <summary>
        /// Search in page titles. Note that Wikipedia does not support this flag.
        /// </summary>
        Title,
        /// <summary>
        /// Search in page text.
        /// </summary>
        Text,
        /// <summary>
        /// Search for a near match in the title. (MediaWiki 1.17+)
        /// </summary>
        NearMatch
    }
}
