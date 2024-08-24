using System.Text.Json.Nodes;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// List all pages using a given page property.
/// </summary>
/// <seealso cref="PropertyName"/>
public class PagesWithPropGenerator : WikiPageGenerator<PagesWithPropResultItem>
{

    public PagesWithPropGenerator(WikiSite site, string propertyName) : base(site)
    {
        PropertyName = propertyName;
    }

    /// <summary>
    /// Page property for which to enumerate pages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list of available properties can be found at
    /// <a href="https://www.mediawiki.org/wiki/Special:ApiSandbox#action=query&amp;list=pagepropnames"><c>action=query&amp;list=pagepropnames</c></a>.
    /// A non-exhaustive example includes
    /// <list type="bullet">
    /// <item><description><c>defaultsort</c></description></item>
    /// <item><description><c>disambiguation</c></description></item>
    /// <item><description><c>displaytitle</c></description></item>
    /// <item><description><c>forcetoc</c></description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets/sets a value that indicates whether the links should be listed in
    /// the descending order. (MediaWiki 1.19+)
    /// </summary>
    public bool OrderDescending { get; set; }

    public override string ListName => "pageswithprop";

    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        return new Dictionary<string, object?>
        {
            { "pwpprop", "ids|title|value" },
            { "pwppropname", PropertyName },
            { "pwplimit", PaginationSize },
            { "pwpdir", OrderDescending ? "descending" : "ascending" },
        };
    }

    protected override PagesWithPropResultItem ItemFromJson(JsonNode json)
    {
        return new PagesWithPropResultItem(MediaWikiHelper.PageStubFromJson(json.AsObject()), (string)json["value"]);
    }

}
