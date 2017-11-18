using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Represents a category on MediaWiki site.
    /// </summary>
    [Obsolete("Use WikiPage instead. To retrieve category statistics, use WikiPage.GetPropertyGroup<CategoryInfoPropertyGroup>().")]
    public class CategoryPage : WikiPage
    {

        public CategoryPage(WikiSite site, string title) : base(site, title, BuiltInNamespaces.Category)
        {
        }

        public int MembersCount => GetPropertyGroup<CategoryInfoPropertyGroup>()?.MembersCount ?? 0;

        public int PagesCount => GetPropertyGroup<CategoryInfoPropertyGroup>()?.PagesCount ?? 0;

        public int FilesCount => GetPropertyGroup<CategoryInfoPropertyGroup>()?.FilesCount ?? 0;

        public int SubcategoriesCount => GetPropertyGroup<CategoryInfoPropertyGroup>()?.SubcategoriesCount ?? 0;

        public IAsyncEnumerable<WikiPage> EnumMembersAsync(PageQueryOptions options)
        {
            return new CategoryMembersGenerator(Site, Title).EnumPagesAsync(options);
        }

        public IAsyncEnumerable<WikiPage> EnumMembersAsync()
        {
            return new CategoryMembersGenerator(Site, Title).EnumPagesAsync();
        }

        public IEnumerable<WikiPage> EnumMembers(PageQueryOptions options)
        {
            return EnumMembersAsync(options).ToEnumerable();
        }

        public IEnumerable<WikiPage> EnumMembers()
        {
            return EnumMembersAsync().ToEnumerable();
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
