using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Search titles and text.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Search .</remarks>
    public class SearchGenerator : PageGenerator<Page>
    {
        private SearchableField _MatchingField = SearchableField.Text;

        private static readonly IList<int> defaultNamespace = new ReadOnlyCollection<int>(new[] {0});

        /// <inheritdoc />
        public SearchGenerator(Site site) : base(site)
        {
        }

        /// <inheritdoc />
        public SearchGenerator(Site site, string keyword) : base(site)
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
        /// <value>The namespace(s) to enumerate. No more than 50 (500 for bots) allowed. Set to <c>null</c> to search
        /// in all the namespaces. (Default: [0], i.e. Main Namespace)</value>
        public IEnumerable<int> NamespaceIds { get; set; } = defaultNamespace;

        /// <summary>
        /// Search inside the text or titles.
        /// </summary>
        /// <remarks>Default: <see cref="SearchableField.Text"/>. This is slightly different from
        /// the MediaWiki default behavior.</remarks>
        public SearchableField MatchingField
        {
            get { return _MatchingField; }
            set
            {
                if (!Enum.IsDefined(typeof(SearchableField), value)) throw new ArgumentOutOfRangeException(nameof(value));
                _MatchingField = value;
            }
        }

        /// <summary>
        /// Include interwiki results in the search, if available. (Default: false, MediaWiki 1.23+)
        /// </summary>
        public bool IncludesInterwiki { get; set; }

        /// <summary>
        /// Class name of search backend to use (Default: $wgSearchType, MediaWiki 1.22+)
        /// </summary>
        public string BackendName { get; set; }

        /// <inheritdoc />
        protected override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams(int actualPagingSize)
        {
            var dict = new Dictionary<string, object>
            {
                {"generator", "search"},
                {"gsrsearch", Keyword},
                {"gsrnamespace", NamespaceIds == null ? "*" : string.Join("|", NamespaceIds)},
                {"gsrwhat", MatchingField},
                {"gsrlimit", actualPagingSize},
            };
            // Include redirect pages in the search. From 1.23 onwards, redirects are always included. (Removed in 1.23)
            if (Site.SiteInfo.Version < new Version(1, 23))
                dict["gsrredirects"] = true;
            switch (MatchingField)
            {
                case SearchableField.Title:
                    dict["gsrwhat"] = "title";
                    break;
                case SearchableField.Text:
                    dict["gsrwhat"] = "text";
                    break;
                case SearchableField.NearMatch:
                    dict["gsrwhat"] = "nearmatch";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return dict;
        }
    }

    /// <summary>
    /// Used in <see cref="SearchGenerator.MatchingField"/>.
    /// </summary>
    public enum SearchableField
    {
        /// <summary>
        /// Search in page titles. Note that Wikipedia does not support this flag.
        /// </summary>
        Title = 0,
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
