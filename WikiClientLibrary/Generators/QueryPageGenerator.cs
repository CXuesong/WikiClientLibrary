using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Get a list provided by a QueryPage-based special page. (MediaWiki 1.18)
/// </summary>
/// <remarks>
/// <para>For a list of available query pages,
/// See <a href="https://www.mediawiki.org/wiki/API:Querypage">mw:API:Querypage</a>.
/// You can also invoke <see cref="GetQueryPageNamesAsync()"/> to ask for the list in the runtime.</para>
/// <para>Not all types of query pages yield list of page titles (e.g., <c>GadgetUsage</c> or <c>MediaStatistics</c>).
/// Invoking <see cref="WikiPageGenerator{TItem}.EnumPagesAsync()"/> may yield pages that does not actually exist.
/// In this case, consider using <see cref="WikiList{T}.EnumItemsAsync"/> instead.</para>
/// </remarks>
public class QueryPageGenerator : WikiPageGenerator<QueryPageResultItem>
{

    /// <inheritdoc />
    public QueryPageGenerator(WikiSite site) : base(site)
    {
    }

    /// <inheritdoc />
    /// <param name="queryPageName">The name of the special page. The name is case sensitive.</param>
    public QueryPageGenerator(WikiSite site, string queryPageName) : base(site)
    {
        QueryPageName = queryPageName;
    }

    /// <summary>
    /// Asynchronously get a list of available QueryPage-based special pages.
    /// The item in the list can later be used as a value of <see cref="QueryPageName"/>.
    /// </summary>
    /// <param name="site">MediaWiki site.</param>
    /// <returns>A list of titles of available QueryPage-based special pages.</returns>
    public static async Task<IList<string>> GetQueryPageNamesAsync(WikiSite site)
    {
        var module = await RequestHelper.QueryParameterInformationAsync(site, "query+querypage");
        var pa = module["parameters"].AsArray().First(p => (string)p["name"] == "page");
        return pa["type"].Deserialize<IList<string>>();
    }

    /// <summary>
    /// Asynchronously get a list of available QueryPage-based special pages.
    /// The item in the list can later be used as a value of <see cref="QueryPageName"/>.
    /// </summary>
    /// <returns>A list of titles of available QueryPage-based special pages.</returns>
    public Task<IList<string>> GetQueryPageNamesAsync()
    {
        return GetQueryPageNamesAsync(Site);
    }

    /// <summary>
    /// Retrieve the basic information of the result set of the currently specified MediaWiki query page.
    /// </summary>
    /// <param name="cancellationToken">a token used to cancel the operation.</param>
    /// <returns>a task that resolves into the query page result set information.</returns>
    /// <exception cref="UnexpectedDataException">Received unexpected JSON data from MediaWiki server.</exception>
    /// <remarks>
    /// To retrieve the items of the query page result set, use <see cref="WikiList{T}.EnumItemsAsync"/>.
    /// </remarks>
    public async Task<QueryPageResultInfo> GetQueryPageResultInfoAsync(CancellationToken cancellationToken = default)
    {
        var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(
            new
            {
                action = "query",
                maxlag = 5,
                list = "querypage",
                qppage = QueryPageName,
                qplimit = 1,
            }), cancellationToken);
        var itemsRoot = RequestHelper.FindQueryResponseItemsRoot(jresult, ListName);
        if (itemsRoot == null) throw new UnexpectedDataException();
        return itemsRoot.Deserialize<QueryPageResultInfo>(MediaWikiHelper.WikiJsonSerializerOptions)
               ?? throw new UnexpectedDataException();
    }

    /// <summary>
    /// Gets/sets the name of the special page. The name is case sensitive.
    /// </summary>
    public string QueryPageName { get; set; } = "";

    /// <inheritdoc />
    public override string ListName => "querypage";

    /// <inheritdoc/>
    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        if (string.IsNullOrWhiteSpace(QueryPageName))
            throw new InvalidOperationException(string.Format(Prompts.ExceptionArgumentNullOrWhitespace1, nameof(QueryPageName)));
        return new Dictionary<string, object?> { { "qppage", QueryPageName }, { "qplimit", PaginationSize } };
    }

    /// <inheritdoc />
    protected override JsonArray? ItemsFromResponse(JsonNode response)
    {
        var itemsRoot = RequestHelper.FindQueryResponseItemsRoot(response, ListName);
        return itemsRoot?["results"]?.AsArray();
    }

    /// <inheritdoc />
    protected override QueryPageResultItem ItemFromJson(JsonNode json)
    {
        return json.Deserialize<QueryPageResultItem>(MediaWikiHelper.WikiJsonSerializerOptions)!;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "QueryPage:" + QueryPageName;
    }

}

// See https://github.com/wikimedia/mediawiki/blob/0833fba86ee47b51799f779ee8611d5cf2ba9c6b/includes/api/ApiQueryQueryPage.php#L101
/// <summary>
/// Contains the basic information of a specific MediaWiki query page result set.
/// </summary>
[JsonContract]
public sealed record QueryPageResultInfo
{

    /// <summary>Name of the query page.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the returned query page result is a cached result set.</summary>
    [JsonPropertyName("cached")]
    public bool IsCached { get; init; }

    /// <summary>Timestamp of the cached result set, if <see cref="IsCached"/> is <c>true</c>.</summary>
    public DateTime CachedTimestamp { get; init; }

    /// <summary>Maximum result count, if available.</summary>
    [JsonPropertyName("maxresults")]
    public int MaxResultCount { get; init; }

}

[JsonContract]
public sealed record QueryPageResultItem
{

    // MaxValue: not resolved yet.
    // MW 1.19- does not have this field.
    [JsonInclude] [JsonPropertyName("timestamp")] private DateTime _Timestamp = DateTime.MaxValue;

    /// <summary>Title of the page, or title of the entry, depending on the nature of query page.</summary>
    public string Title { get; init; } = "";

    /// <summary>Namespace of the page.</summary>
    [JsonPropertyName("ns")]
    public int NamespaceId { get; init; }

    /// <summary>Value associated with the page or the entry specified by <see cref="Title"/>, if applicable.</summary>
    /// <remarks>
    /// Depending on the nature of different types of query pages, this property can be
    /// <list type="bullet">
    /// <item><term>stringified number</term>
    /// <description>
    /// referencing page count (Mostcategories, Mostimages, Mostinterwikis, Mostlinked, Wantedfiles, Wantedpages, etc.),
    /// revision count (Fewestrevisions, etc.),
    /// page length (Shortpage, Longpage),
    /// usage count (GadgetUsage),
    /// etc.
    /// </description>
    /// </item>
    /// <item><term>stringified UNIX timestamp</term>
    /// <description>page time stamp (Ancientpages, etc.); consider retrieving the timestamp from <see cref="Timestamp"/> or leveraging <see cref="DateTimeOffset.FromUnixTimeSeconds"/> to parse the value.</description>
    /// </item>
    /// <item><term>etc.</term>
    /// <description><c>"0"</c>, <c>"1"</c>, or stringified sequence number.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public string Value { get; init; } = "";

    /// <summary>
    /// Timestamp associated with the query page result item, if applicable.
    /// </summary>
    /// <value>timestamp value of the entry, or <see cref="DateTime.MinValue"/> if there is no such information available.</value>
    /// <remarks>
    /// Only access this property when you already know the query page you are accessing yields valid timestamp values.
    /// </remarks>
    [JsonIgnore]
    public DateTime Timestamp
    {
        get
        {
            if (_Timestamp != DateTime.MaxValue) return _Timestamp;
            if (long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
            {
                // 1990-01-01 0:0:0 ~ 9999-12-31 23:59:59
                if (epoch is > 631152000 and <= 253402300799)
                {
                    return _Timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                }
            }
            return _Timestamp = DateTime.MinValue;
        }
    }

    /// <summary>
    /// If applicable, retrieves a wiki page stub from the current entry.
    /// </summary>
    /// <value>
    /// a wiki page stub with <see cref="Title"/> and <see cref="NamespaceId"/> information from in this class.
    /// <see cref="WikiPageStub.Id"/> is not assigned, and <see cref="WikiPageStub.IsMissing"/> is always <c>false</c>.
    /// </value>
    /// <remarks>
    /// Only access this property when you already know the query page you are accessing yields valid page titles.
    /// </remarks>
    public WikiPageStub PageStub => new WikiPageStub(Title, NamespaceId);

    /// <inheritdoc />
    public override string ToString()
        => $"[{NamespaceId}]{Title}: {(Timestamp != DateTime.MinValue ? Timestamp.ToString("u") : Value)}";

}
