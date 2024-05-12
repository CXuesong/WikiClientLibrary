using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Get all pages on the current user's watchlist.
/// </summary>
public class MyWatchlistGenerator : WikiPageGenerator<MyWatchlistResultItem>
{

    public MyWatchlistGenerator(WikiSite site) : base(site)
    {
    }

    /// <summary>
    /// Only list pages in the given namespaces.
    /// </summary>
    public IEnumerable<int>? NamespaceIds { get; set; }

    /// <summary>
    /// Only show pages that have not been changed.
    /// </summary>
    public PropertyFilterOption NotChangedPagesFilter { get; set; }

    /// <summary>
    /// Gets/sets a value that indicates whether the links should be listed in
    /// the descending order. (MediaWiki 1.19+)
    /// </summary>
    public bool OrderDescending { get; set; }

    /// <summary>
    /// Title (with namespace prefix) to begin enumerating from.
    /// </summary>
    public string? FromTitle { get; set; }

    /// <summary>
    /// Title (with namespace prefix) to stop enumerating at.
    /// </summary>
    public string? ToTitle { get; set; }

    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        return new Dictionary<string, object?>
        {
            { "wrnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds) },
            { "wrlimit", PaginationSize },
            { "wrprop", "changed" },
            { "wrshow", NotChangedPagesFilter.ToString("!changed", "changed", null) },
            { "wrdir", OrderDescending ? "descending" : "ascending" },
            { "wrfromtitle", FromTitle },
            { "wrtotitle", ToTitle },
        };
    }

    protected override MyWatchlistResultItem ItemFromJson(JToken json)
    {
        DateTime? changedTime = null;
        if (json["changed"] != null)
        {
            changedTime = DateTime.Parse((string)json["changed"]);
        }

        return new MyWatchlistResultItem(MediaWikiHelper.PageStubFromJson((JObject)json), json["changed"] != null, changedTime);
    }

    public override string ListName => "watchlistraw";

}
