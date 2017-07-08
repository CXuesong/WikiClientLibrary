using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Represents a category on MediaWiki site.
    /// </summary>
    public class Category : Page
    {

        public Category(Site site, string title) : base(site, title, BuiltInNamespaces.Category)
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
                MembersCount = (int) cat["size"];
                PagesCount = (int) cat["pages"];
                FilesCount = (int) cat["files"];
                SubcategoriesCount = (int) cat["subcats"];
            }
            else
            {
                // Possibly not a valid category.
                MembersCount = PagesCount = FilesCount = SubcategoriesCount = 0;
            }
        }

        public int MembersCount { get; private set; }

        public int PagesCount { get; private set; }

        public int FilesCount { get; private set; }

        public int SubcategoriesCount { get; private set; }

        public IAsyncEnumerable<Page> EnumMembersAsync(PageQueryOptions options)
        {
            return new CategoryMembersGenerator(Site, Title).EnumPagesAsync(options);
        }

        public IAsyncEnumerable<Page> EnumMembersAsync()
        {
            return new CategoryMembersGenerator(Site, Title).EnumPagesAsync();
        }

        public IEnumerable<Page> EnumMembers(PageQueryOptions options)
        {
            return new CategoryMembersGenerator(Site, Title).EnumPages(options);
        }

        public IEnumerable<Page> EnumMembers()
        {
            return new CategoryMembersGenerator(Site, Title).EnumPages();
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{Title}, M:{MembersCount}, P:{PagesCount}, S:{SubcategoriesCount}, F:{FilesCount}";
        }
    }
}
