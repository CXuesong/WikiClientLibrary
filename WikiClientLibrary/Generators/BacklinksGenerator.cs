using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates all the pages that links to the specified title (not transclusion). (aka. what-links-here)
    /// </summary>
    /// <seealso cref="TranscludedInGenerator"/>
    public class BacklinksGenerator : WikiPageGenerator<WikiPage>
    {
        /// <inheritdoc />
        public BacklinksGenerator(WikiSite site) : base(site)
        {
        }

        public BacklinksGenerator(WikiSite site, string targetTitle) : base(site)
        {
            TargetTitle = targetTitle;
        }

        /// <summary>
        /// List pages linking to this title. The title does not need to exist.
        /// </summary>
        public string TargetTitle { get; set; }

        /// <summary>
        /// List pages linking to this page ID.
        /// </summary>
        public int? TargetPageId { get; set; }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>Selected ids of namespace, or <c>null</c> if all the namespaces are selected.</value>
        public IEnumerable<int> NamespaceIds { get; set; }

        /// <summary>
        /// How to filter redirects in the results.
        /// </summary>
        public PropertyFilterOption RedirectsFilter { get; set; }

        /// <summary>
        /// Whether pages linking to the specified target through a redirect should also be listed.
        /// </summary>
        public bool AllowRedirectedLinks { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams(int actualPagingSize)
        {
            if ((TargetTitle != null) == (TargetPageId != null))
                throw new ArgumentException("Either TargetTitle and TargetPageId should be null, not both, nor none.");
            var pagingSize = actualPagingSize;
            if (AllowRedirectedLinks)
            {
                // When the blredirect parameter is set, this module behaves slightly differently.
                // bllimit applies to both levels separately: if e.g. bllimit=10,
                // at most 10 first-level pages (pages that link to bltitle) and
                // 10 second-level pages (pages that link to bltitle through a redirect) will be listed.
                // Continuing queries also works differently.
                pagingSize = Math.Max(1, actualPagingSize / 2);
            }
            return new Dictionary<string, object>
            {
                {"generator", "backlinks"},
                {"gbltitle", TargetTitle},
                {"gblpageid", TargetPageId},
                {"gblnamespace", NamespaceIds == null ? null : string.Join("|", NamespaceIds)},
                {"gblfilterredir", RedirectsFilter.ToString("redirects", "nonredirects")},
                {"gbllimit", pagingSize},
                {"gblredirect", AllowRedirectedLinks}
            };
        }
    }
}
