using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates pages from all links on the provided page.
    /// (<a href="https://www.mediawiki.org/wiki/API:Links">mw:API:Links</a>, MediaWiki 1.11+)
    /// </summary>
    /// <see cref="TransclusionsGenerator"/>
    /// <see cref="BacklinksGenerator"/>
    public class LinksGenerator : WikiPagePropertyGenerator
    {
        /// <inheritdoc />
        public LinksGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        /// <param name="pageTitle">The page title from which to enumerate links.</param>
        public LinksGenerator(WikiSite site, string pageTitle) : base(site)
        {
            PageTitle = pageTitle;
        }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>selected IDs of namespace, or <c>null</c> to select all the namespaces.</value>
        public IEnumerable<int>? NamespaceIds { get; set; }

        /// <summary>
        /// Only list links to these titles. Useful for checking whether a certain page links to a certain title.
        /// (MediaWiki 1.17+)
        /// </summary>
        /// <value>a sequence of page titles, or <c>null</c> to list all the linked pages.</value>
        public IEnumerable<string>? MatchingTitles { get; set; }

        /// <summary>
        /// Gets/sets a value that indicates whether the links should be listed in
        /// the descending order. (MediaWiki 1.19+)
        /// </summary>
        public bool OrderDescending { get; set; }

        /// <inheritdoc />
        public override string PropertyName => "links";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
        {
            return new Dictionary<string, object?>
            {
                {"plnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds)}, {"pllimit", PaginationSize}, {"pltitles", MatchingTitles == null ? null : MediaWikiHelper.JoinValues(MatchingTitles)},
                {"pldir", OrderDescending ? "descending" : "ascending"}
            };
        }
    }
}
