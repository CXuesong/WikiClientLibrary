using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates pages from all pages (typically templates) transcluded in the provided page.
    /// (<a href="https://www.mediawiki.org/wiki/API:Templates">mw:API:Templates</a>, MediaWiki 1.11+)
    /// </summary>
    /// <seealso cref="LinksGenerator"/>
    /// <see cref="TranscludedInGenerator"/>
    public class TransclusionsGenerator : WikiPagePropertyGenerator<WikiPage>
    {
        /// <inheritdoc />
        public TransclusionsGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        /// <param name="pageTitle">The page title from which to enumerate links.</param>
        public TransclusionsGenerator(WikiSite site, string pageTitle) : base(site)
        {
            PageTitle = pageTitle;
        }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>Selected IDs of namespace, or <c>null</c> if all the namespaces are selected.</value>
        public IEnumerable<int> NamespaceIds { get; set; }

        /// <summary>
        /// Only list transclusion to these titles. Useful for checking whether a certain page links to a certain title.
        /// (MediaWiki 1.17+)
        /// </summary>
        /// <value>A sequence of page titles, or <c>null</c> to list all the linked pages.</value>
        public IEnumerable<string> MatchingTitles { get; set; }

        /// <summary>
        /// Gets/sets a value that indicates whether the links should be listed in
        /// the descending order. (MediaWiki 1.19+)
        /// </summary>
        public bool OrderDescending { get; set; }

        /// <inheritdoc />
        public override string PropertyName => "templates";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            return new Dictionary<string, object>
            {
                {"tlnamespace", NamespaceIds == null ? null : string.Join("|", NamespaceIds)},
                {"tllimit", PaginationSize},
                {"tltemplates", MatchingTitles == null ? null : string.Join("|", MatchingTitles)},
                {"tldir", OrderDescending ? "descending" : "ascending"}
            };
        }
    }
}
