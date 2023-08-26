using System.Collections.Generic;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates pages from all used files on the provided page.
    /// (<a href="https://www.mediawiki.org/wiki/API:Links">mw:API:Links</a>, MediaWiki 1.11+)
    /// </summary>
    /// <see cref="TransclusionsGenerator"/>
    /// <see cref="BacklinksGenerator"/>
    /// <see cref="LinksGenerator"/>
    public class FilesGenerator : WikiPagePropertyGenerator
    {
        /// <inheritdoc />
        public FilesGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        public FilesGenerator(WikiSite site, WikiPageStub pageStub) : base(site, pageStub)
        {
        }

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
        public override string PropertyName => "images";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
        {
            return new Dictionary<string, object?>
            {
                {"imlimit", PaginationSize}, 
                {"imtitles", MatchingTitles == null ? null : MediaWikiHelper.JoinValues(MatchingTitles)},
                {"imdir", OrderDescending ? "descending" : "ascending"}
            };
        }
    }
}