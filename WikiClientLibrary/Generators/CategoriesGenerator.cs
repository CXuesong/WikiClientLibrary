using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Gets a list of all categories used on the provided pages.
    /// (<a href="https://www.mediawiki.org/wiki/API:Categories">mw:API:Categories</a>, MediaWiki 1.11+)
    /// </summary>
    public class CategoriesGenerator : WikiPagePropertyGenerator<WikiPageCategoryInfo>
    {
        /// <inheritdoc />
        public CategoriesGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        public CategoriesGenerator(WikiSite site, WikiPageStub pageStub) : base(site, pageStub)
        {
        }

        /// <summary>
        /// Whether to include hidden categories in the returned list.
        /// </summary>
        public PropertyFilterOption HiddenCategoryFilter { get; set; }

        /// <summary>
        /// Only list these categories. Useful for checking whether a certain page is in a certain category.
        /// </summary>
        public IEnumerable<string>? CategorySelection { get; set; }

        /// <inheritdoc />
        public override string PropertyName => "categories";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
        {
            var p = new OrderedKeyValuePairs<string, object?>
            {
                {"clprop", "sortkey|timestamp|hidden"},
                {"clshow", HiddenCategoryFilter.ToString("hidden", "!hidden", null)},
                {"cllimit", PaginationSize},
            };
            if (CategorySelection != null) p.Add("clcategories", MediaWikiHelper.JoinValues(CategorySelection));
            return p;
        }

        /// <inheritdoc />
        protected override WikiPageCategoryInfo ItemFromJson(JToken json, JObject jpage)
        {
            return CategoriesPropertyGroup.CategoryInfoFromJson(json);
        }
    }
}
