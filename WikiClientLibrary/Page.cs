using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a page on MediaWiki site.
    /// </summary>
    public class Page
    {
        public Site Site { get; }

        public string Title { get; private set; }

        public Page(Site site, string title)
        {
            Site = site;
            Title = title;
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (title == null) throw new ArgumentNullException(nameof(title));
        }
    }
}
