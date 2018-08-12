using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Extension method for constructing generators from <see cref="WikiPage"/>.
    /// </summary>
    public static class WikiPageExtensions
    {

        /// <summary>
        /// Creates a <see cref="LinksGenerator"/> instance from the specified page,
        /// which generates pages from all links on the page.
        /// </summary>
        /// <param name="page">The page.</param>
        public static LinksGenerator CreateLinksGenerator(this WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new LinksGenerator(page.Site, page.Title);
        }

        /// <summary>
        /// Creates a <see cref="TransclusionsGenerator"/> instance from the specified page,
        /// which generates pages from all pages (typically templates) transcluded in the page.
        /// </summary>
        /// <param name="page">The page.</param>
        public static TransclusionsGenerator CreateTransclusionsGenerator(this WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new TransclusionsGenerator(page.Site, page.Title);
        }

        /// <summary>
        /// Creates a <see cref="RevisionsGenerator"/> instance from the specified page,
        /// which enumerates the sequence of revisions on the page.
        /// </summary>
        /// <param name="page">The page.</param>
        public static RevisionsGenerator CreateRevisionsGenerator(this WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new RevisionsGenerator(page.Site, page.Title);
        }

    }
}
