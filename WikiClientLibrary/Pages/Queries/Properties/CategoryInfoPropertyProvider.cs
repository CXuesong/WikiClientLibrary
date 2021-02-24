using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    /// <summary>
    /// Provides information for category pages.
    /// (<a href="https://www.mediawiki.org/wiki/API:Categoryinfo">mw:API:Categoryinfo</a>, MediaWiki 1.13+)
    /// </summary>
    public class CategoryInfoPropertyProvider : WikiPagePropertyProvider<CategoryInfoPropertyGroup>
    {

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
        {
            return Enumerable.Empty<KeyValuePair<string, object?>>();
        }

        /// <inheritdoc />
        public override CategoryInfoPropertyGroup? ParsePropertyGroup(JObject json)
        {
            return CategoryInfoPropertyGroup.Create(json);
        }

        /// <inheritdoc />
        public override string? PropertyName => "categoryinfo";
    }

    /// <summary>
    /// Property group for category page information.
    /// </summary>
    /// <remarks>
    /// For the categories that has sub-items but without category page, this property group is still valid.
    /// </remarks>
    public class CategoryInfoPropertyGroup : WikiPagePropertyGroup
    {

        public static CategoryInfoPropertyGroup? Create(JObject jPage)
        {
            var cat = jPage["categoryinfo"];
            // jpage["imageinfo"] == null indicates the page may not be a valid Category.
            if (cat == null) return null;
            return new CategoryInfoPropertyGroup(cat);
        }

        private CategoryInfoPropertyGroup(JToken jCategoryInfo)
        {
            MembersCount = (int)jCategoryInfo["size"];
            PagesCount = (int)jCategoryInfo["pages"];
            FilesCount = (int)jCategoryInfo["files"];
            SubcategoriesCount = (int)jCategoryInfo["subcats"];
        }

        /// <summary>Count of members in this category.</summary>
        public int MembersCount { get; }

        /// <summary>Count of pages in this category.</summary>
        public int PagesCount { get; }

        /// <summary>Count of files in this category.</summary>
        public int FilesCount { get; }

        /// <summary>Count of sub-categories in this category.</summary>
        public int SubcategoriesCount { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"M:{MembersCount}, P:{PagesCount}, S:{SubcategoriesCount}, F:{FilesCount}";
        }
    }

}
