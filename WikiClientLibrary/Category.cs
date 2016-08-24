using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a category on MediaWiki site.
    /// </summary>
    public class Category : Page
    {
        public Category(Site site, string title) : base(site, title)
        {
        }

        internal Category(Site site) : base(site)
        {
        }

        protected override void OnLoadPageInfo(JObject jpage)
        {
            base.OnLoadPageInfo(jpage);
            var cat = jpage["categoryinfo"];
            if (cat != null)
            {
                ChildrenCount = (int) cat["size"];
                PagesCount = (int) cat["pages"];
                FilesCount = (int) cat["files"];
                SubcategoriesCount = (int) cat["subcats"];
            }
            else
            {
                // Possibly not a valid category.
                ChildrenCount = PagesCount = FilesCount = SubcategoriesCount = 0;
            }
        }

        public int ChildrenCount { get; private set; }

        public int PagesCount { get; private set; }

        public int FilesCount { get; private set; }

        public int SubcategoriesCount { get; private set; }

    }
}
