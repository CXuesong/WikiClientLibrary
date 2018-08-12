using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates all the pages in a specific namespace.
    /// </summary>
    /// <remarks>
    /// To enumerate all the categories used on a wiki site,
    /// along with those without existing category pages, use <see cref="CategoriesGenerator"/>.
    /// </remarks>
    /// <seealso cref="RandomPageGenerator"/>
    public class AllPagesGenerator : WikiPageGenerator
    {

        /// <inheritdoc />
        public AllPagesGenerator(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// List all the pages in this namespace.
        /// </summary>
        public int NamespaceId { get; set; } = BuiltInNamespaces.Main;

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
        public override string ListName => "allpages";

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            return new Dictionary<string, object>
            {
                {"apfrom", StartTitle},
                {"apto", EndTitle},
                {"aplimit", PaginationSize},
                {"apnamespace", NamespaceId},
                {"apprefix", Prefix},
                {"apfilterredir", RedirectsFilter.ToString("redirects", "nonredirects")},
                {"apfilterlanglinks", LanguageLinkFilter.ToString("withlanglinks", "withoutlanglinks")},
                {"apminsize", MinPageContentLength},
                {"apmaxsize", MaxPageContentLength},
                // TODO add other filters
            };
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