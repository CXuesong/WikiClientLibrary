using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties;

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
            { "clprop", "sortkey|timestamp|hidden" }, { "clshow", HiddenCategoryFilter.ToString("hidden", "!hidden", null) },
        };
        if (CategorySelection != null) p.Add("clcategories", MediaWikiHelper.JoinValues(CategorySelection));
        return p;
    }

    /// <inheritdoc />
    public override CategoriesPropertyGroup? ParsePropertyGroup(JsonObject json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return CategoriesPropertyGroup.Create(json["categories"]?.AsArray());
    }

    /// <inheritdoc />
    public override string? PropertyName => "categories";

}

/// <summary>
/// Contains information about a page's belonging category.
/// </summary>
public sealed class WikiPageCategoryInfo
{

    /// <summary>
    /// Page information for the category.
    /// </summary>
    /// <remarks>The <see cref="WikiPageStub.HasId"/> of the returned <see cref="WikiPageStub"/> is <c>false</c>.</remarks>
    public required WikiPageStub Page { get; init; }

    /// <summary>
    /// Full name of the category, with namespace prefix.
    /// </summary>
    public string? Title => Page.HasTitle ? Page.Title : null;

    /// <summary>Gets a value that indicates whether the category is hidden.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Gets the sortkey prefix (human-readable part) of the current page in the category.</summary>
    public string? SortKey { get; init; }

    /// <summary>Gets the sortkey (hexadecimal string) of the current page in the category.</summary>
    public string? FullSortKey { get; init; }

    /// <summary>Gets the timestamp of when the category was added.</summary>
    /// <value>A timestamp, or <see cref="DateTime.MinValue"/> if not available.</value>
    public DateTime TimeStamp { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Page.ToString();
    }

}

public class CategoriesPropertyGroup : WikiPagePropertyGroup
{

    private static readonly CategoriesPropertyGroup empty = new();

    internal static CategoriesPropertyGroup Create(JsonArray? jcats)
    {
        if (jcats == null || jcats.Count == 0) return empty;
        return new CategoriesPropertyGroup(jcats);
    }

    private CategoriesPropertyGroup()
    {
        Categories = Array.Empty<WikiPageCategoryInfo>();
    }

    private CategoriesPropertyGroup(JsonArray jcats)
    {
        Categories = new ReadOnlyCollection<WikiPageCategoryInfo>(jcats
            .Where(n => n != null)
            .Select(CategoryInfoFromJson!).ToList()
        );
    }

    internal static WikiPageCategoryInfo CategoryInfoFromJson(JsonNode json)
    {
        return new WikiPageCategoryInfo
        {
            Page = MediaWikiHelper.PageStubFromJson(json.AsObject()),
            IsHidden = json["hidden"] != null,
            SortKey = (string?)json["sortkeyprefix"],
            FullSortKey = (string?)json["sortkey"],
            TimeStamp = (DateTime?)json["timestamp"] ?? DateTime.MinValue,
        };
    }

    public IReadOnlyCollection<WikiPageCategoryInfo> Categories { get; }

}
