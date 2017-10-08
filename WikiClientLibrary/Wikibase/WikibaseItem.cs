using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// An item usually indicating an entity in the Wikibase.
    /// </summary>
    internal class WikibaseItem : WikiPage      // Reserved for future use.
    {
        /// <inheritdoc />
        public WikibaseItem(WikiSite site, string title) : base(site, title)
        {
        }

        /// <inheritdoc />
        public WikibaseItem(WikiSite site, string title, int defaultNamespaceId) : base(site, title, defaultNamespaceId)
        {
        }

        /// <inheritdoc />
        internal WikibaseItem(WikiSite site) : base(site)
        {
        }
    }
}
