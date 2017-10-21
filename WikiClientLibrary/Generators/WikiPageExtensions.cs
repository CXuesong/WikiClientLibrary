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

        public static LinksGenerator CreateLinksGenerator(this WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new LinksGenerator(page.Site, page.Title);
        }

        public static TransclusionsGenerator CreateTransclusionsGenerator(this WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new TransclusionsGenerator(page.Site, page.Title);
        }

        public static RevisionsGenerator CreateRevisionsGenerator(this WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return new RevisionsGenerator(page.Site, page.Title);
        }

    }
}
