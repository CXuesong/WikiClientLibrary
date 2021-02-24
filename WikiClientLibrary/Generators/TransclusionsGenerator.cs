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
    /// Generates pages from all pages (typically templates) transcluded in the provided page.
    /// (<a href="https://www.mediawiki.org/wiki/API:Templates">mw:API:Templates</a>, MediaWiki 1.11+)
    /// </summary>
    /// <seealso cref="LinksGenerator"/>
    /// <seealso cref="TranscludedInGenerator"/>
    public class TransclusionsGenerator : WikiPagePropertyGenerator
    {
        /// <inheritdoc />
        public TransclusionsGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        public TransclusionsGenerator(WikiSite site, WikiPageStub pageStub) : base(site, pageStub)
        {
        }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>Selected IDs of namespace, or <c>null</c> if all the namespaces are selected.</value>
        public IEnumerable<int>? NamespaceIds { get; set; }

        /// <summary>
        /// Only list transclusion to these titles. Useful for checking whether a certain page links to a certain title.
        /// (MediaWiki 1.17+)
        /// </summary>
        /// <value>A sequence of page titles, or <c>null</c> to list all the linked pages.</value>
        public IEnumerable<string>? MatchingTitles { get; set; }

        /// <summary>
        /// Gets/sets a value that indicates whether the links should be listed in
        /// the descending order. (MediaWiki 1.19+)
        /// </summary>
        public bool OrderDescending { get; set; }

        /// <inheritdoc />
        public override string PropertyName => "templates";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
        {
            return new Dictionary<string, object?>
            {
                { "tlnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds) },
                { "tllimit", PaginationSize },
                { "tltemplates", MatchingTitles == null ? null : MediaWikiHelper.JoinValues(MatchingTitles) },
                { "tldir", OrderDescending ? "descending" : "ascending" }
            };
        }
    }
}
