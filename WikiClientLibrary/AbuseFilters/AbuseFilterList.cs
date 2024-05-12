using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.AbuseFilters;

public class AbuseFilterList : WikiList<AbuseFilter>
{
    /// <inheritdoc />
    public AbuseFilterList(WikiSite site) : base(site)
    {
        }

    /// <summary>The filter ID to start enumerating from.</summary>
    public int StartId { get; set; }

    /// <summary>The filter ID to stop enumerating at.</summary>
    public int EndId { get; set; }

    /// <inheritdoc />
    public override string ListName => "abusefilters";

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
            // TODO abfshow
            return new Dictionary<string, object?>
            {
                {"abfprop", "id|pattern|description|actions|comments|lasteditor|lastedittime|private|status|hits"},
                {"abflimit", PaginationSize},
            };
        }

    /// <inheritdoc />
    protected override AbuseFilter ItemFromJson(JToken json)
    {
            return json.ToObject<AbuseFilter>(Utility.WikiJsonSerializer);
        }
}