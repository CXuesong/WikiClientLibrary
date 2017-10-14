using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates all the pages in a specific namespace.
    /// </summary>
    public class AllPagesGenerator : WikiPageGenerator<WikiPage>
    {
        public int NamespaceId { get; set; } = 0;

        /// <summary>
        /// Start listing at this title. The title need not exist.
        /// </summary>
        public string StartTitle { get; set; } = "!";

        /// <summary>
        /// The page title to stop enumerating at.
        /// </summary>
        public string EndTitle { get; set; } = null;

        /// <summary>
        /// Only list titles that start with this value.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// How to filter redirects.
        /// </summary>
        public PropertyFilterOption RedirectsFilter { get; set; }

        /// <summary>
        /// How to filter based on whether a page has language links.
        /// </summary>
        public PropertyFilterOption LanguageLinkFilter { get; set; }

        /// <summary>
        /// Only list pages that are at least this many bytes in size.
        /// </summary>
        public int? MinPageContentLength { get; set; }

        /// <summary>
        /// Only list pages that are at most this many bytes in size.
        /// </summary>
        public int? MaxPageContentLength { get; set; }

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams(int actualPagingSize)
        {
            return new Dictionary<string, object>
            {
                {"generator", "allpages"},
                {"gapfrom", StartTitle},
                {"gapto", EndTitle},
                {"gaplimit", actualPagingSize},
                {"gapnamespace", NamespaceId},
                {"gapprefix", Prefix},
                {"gapfilterredir", RedirectsFilter.ToString("redirects", "nonredirects")},
                {"gapfilterlanglinks", LanguageLinkFilter.ToString("withlanglinks", "withoutlanglinks")},
                {"gapminsize", MinPageContentLength},
                {"gapmaxsize", MaxPageContentLength},
                // TODO add other filters
            };
        }

        public AllPagesGenerator(WikiSite site) : base(site)
        {
        }
    }

    /// <summary>
    /// Determines whether a page with/without certain
    /// specific property should be included in the list.
    /// </summary>
    public enum PropertyFilterOption
    {
        /// <summary>
        /// Do not filter by this property.
        /// </summary>
        Disable = 0,
        /// <summary>
        /// Only include the pages with this property.
        /// </summary>
        WithProperty,
        /// <summary>
        /// Only include the pages without this property.
        /// </summary>
        WithoutProperty,
    }
}