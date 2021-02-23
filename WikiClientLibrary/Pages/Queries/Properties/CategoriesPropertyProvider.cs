using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    public class CategoriesPropertyProvider : WikiPagePropertyProvider<CategoriesPropertyGroup>
    {

        /// <summary>
        /// Whether to include hidden categories in the returned list.
        /// </summary>
        public PropertyFilterOption HiddenCategoryFilter { get; set; }

        /// <summary>
        /// Only list these categories. Useful for checking whether a certain page is in a certain category.
        /// </summary>
        public IEnumerable<string>? CategorySelection { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
        {
            var p = new OrderedKeyValuePairs<string, object?>
            {
                {"clprop", "sortkey|timestamp|hidden"},
                {"clshow", HiddenCategoryFilter.ToString("hidden", "!hidden", null)}
            };
            if (CategorySelection != null) p.Add("clcategories", MediaWikiHelper.JoinValues(CategorySelection));
            return p;
        }

        /// <inheritdoc />
        public override CategoriesPropertyGroup ParsePropertyGroup(JObject json)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override string? PropertyName => "categories";
    }

    /// <summary>
    /// Contains information about a page's belonging category.
    /// </summary>
    public readonly struct WikiPageCategoryInfo
    {
        public WikiPageCategoryInfo(WikiPageStub page, bool isHidden, string sortKey, string fullSortKey, DateTime timeStamp)
        {
            Page = page;
            IsHidden = isHidden;
            SortKey = sortKey;
            TimeStamp = timeStamp;
            FullSortKey = fullSortKey;
        }

        /// <summary>
        /// Page information for the category.
        /// </summary>
        /// <remarks>The <see cref="WikiPageStub.HasId"/> of the returned <see cref="WikiPageStub"/> is <c>false</c>.</remarks>
        public WikiPageStub Page { get; }

        /// <summary>
        /// Full name of the category.
        /// </summary>
        public string? Title => Page.HasTitle ? Page.Title : null;

        /// <summary>Gets a value that indicates whether the category is hidden.</summary>
        public bool IsHidden { get; }

        /// <summary>Gets the sortkey prefix (human-readable part) of the current page in the category.</summary>
        public string SortKey { get; }

        /// <summary>Gets the sortkey (hexadecimal string) of the current page in the category.</summary>
        public string FullSortKey { get; }

        /// <summary>Gets the timestamp of when the category was added.</summary>
        /// <value>A timestamp, or <see cref="DateTime.MinValue"/> if not available.</value>
        public DateTime TimeStamp { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Page.ToString();
        }
    }

    public class CategoriesPropertyGroup : WikiPagePropertyGroup
    {

        private static readonly CategoriesPropertyGroup empty = new CategoriesPropertyGroup();

        internal CategoriesPropertyGroup Create(JArray jcats)
        {
            if (jcats == null) return null;
            if (!jcats.HasValues) return empty;
            return new CategoriesPropertyGroup(jcats);
        }

        private CategoriesPropertyGroup()
        {
            Categories = Array.Empty<WikiPageCategoryInfo>();
        }

        private CategoriesPropertyGroup(JArray jcats)
        {
            Categories = new ReadOnlyCollection<WikiPageCategoryInfo>(jcats.Select(CategoryInfoFromJson).ToList());
        }

        internal static WikiPageCategoryInfo CategoryInfoFromJson(JToken json)
        {
            return new WikiPageCategoryInfo(MediaWikiHelper.PageStubFromJson((JObject)json),
                json["hidden"] != null, (string)json["sortkeyprefix"], (string)json["sortkey"],
                (DateTime?)json["timestamp"] ?? DateTime.MinValue);
        }

        public IReadOnlyCollection<WikiPageCategoryInfo> Categories { get; }

    }

}
