using System.ComponentModel;
using System.Text.Json.Nodes;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Enumerates the sequence of revisions on a specific MediaWiki page.
/// (<a href="https://www.mediawiki.org/wiki/API:Revisions">mw:API:Revisions</a>, MediaWiki 1.8+)
/// </summary>
/// <remarks>
/// The <c>prop=revisions</c> module has been implemented as
/// <see cref="RevisionsPropertyProvider"/> and <see cref="RevisionsGenerator"/>.
/// The former allows you to fetch for the latest revisions for multiple pages,
/// while the latter allows you to enumerate the revisions of a single page.
/// </remarks>
public class RevisionsGenerator : WikiPagePropertyGenerator<Revision>
{

    private RevisionsPropertyProvider _PropertyProvider = new RevisionsPropertyProvider();

    /// <inheritdoc />
    public RevisionsGenerator(WikiSite site) : base(site)
    {
    }

    /// <inheritdoc />
    public RevisionsGenerator(WikiSite site, WikiPageStub pageStub) : base(site, pageStub)
    {
    }

    /// <inheritdoc />
    public override string PropertyName => "revisions";

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        var p = new OrderedKeyValuePairs<string, object?>
        {
            { "rvlimit", PaginationSize },
            { "rvdir", TimeAscending ? "newer" : "older" },
            { "rvstart", StartTime },
            { "rvend", EndTime },
            { "rvstartid", StartRevisionId },
            { "rvendid", EndRevisionId },
            { "rvuser", UserName },
            { "rvexcludeuser", ExcludedUserName },
        };
        p.AddRange(_PropertyProvider.EnumParameters(Site.SiteInfo.Version));
        return p;
    }

    /// <inheritdoc />
    protected override Revision ItemFromJson(JsonNode json, JsonObject jpage)
    {
        return MediaWikiHelper.RevisionFromJson(json.AsObject(), MediaWikiHelper.PageStubFromJson(jpage));
    }

    /// <summary>
    /// Whether to list revisions in ascending order of time.
    /// </summary>
    /// <value><c>true</c>, if oldest revisions are listed first; or <c>false</c>, if newest revisions are listed first.</value>
    /// <remarks>
    /// Any specified <see cref="StartTime"/> value must be later than any specified <see cref="EndTime"/> value.
    /// This requirement is reversed if <see cref="TimeAscending"/> is <c>true</c>.
    /// </remarks>
    public bool TimeAscending { get; set; } = false;

    /// <summary>
    /// The timestamp to start listing from.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// The timestamp to end listing at.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Revision ID to start listing from.
    /// </summary>
    public long? StartRevisionId { get; set; }

    /// <summary>
    /// Revision ID to stop listing at. 
    /// </summary>
    public long? EndRevisionId { get; set; }

    /// <summary>
    /// Only list revisions made by this user.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Do not list revisions made by this user.
    /// </summary>
    public string? ExcludedUserName { get; set; }

    /// <summary>
    /// Gets/sets the page query options for <see cref="WikiPagePropertyList{T}.EnumItemsAsync"/>
    /// </summary>
    public RevisionsPropertyProvider PropertyProvider
    {
        get { return _PropertyProvider; }
        set { _PropertyProvider = value ?? new RevisionsPropertyProvider(); }
    }

    /// <inheritdoc />
    /// <summary>Infrastructure. Not intended to be used directly in your code.
    /// Asynchronously enumerates the pages from generator.</summary>
    /// <remarks>
    /// Using <c>revisions</c> as generator is not supported until MediaWiki 1.25.
    /// Usually this generator will only return the title specified in
    /// <see cref="WikiPagePropertyList{T}.PageTitle"/> or <see cref="WikiPagePropertyList{T}.PageId"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override IAsyncEnumerable<WikiPage> EnumPagesAsync()
    {
        return base.EnumPagesAsync();
    }

    /// <inheritdoc />
    /// <summary>Infrastructure. Not intended to be used directly in your code.
    /// Asynchronously enumerates the pages from generator.</summary>
    /// <remarks>
    /// Using <c>revisions</c> as generator is not supported until MediaWiki 1.25.
    /// Usually this generator will only return the title specified in
    /// <see cref="WikiPagePropertyList{T}.PageTitle"/> or <see cref="WikiPagePropertyList{T}.PageId"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override IAsyncEnumerable<WikiPage> EnumPagesAsync(PageQueryOptions options)
    {
        return base.EnumPagesAsync(options);
    }

    /// <inheritdoc />
    /// <summary>Infrastructure. Not intended to be used directly in your code.
    /// Asynchronously enumerates the pages from generator.</summary>
    /// <remarks>
    /// Using <c>revisions</c> as generator is not supported until MediaWiki 1.25.
    /// Usually this generator will only return the title specified in
    /// <see cref="WikiPagePropertyList{T}.PageTitle"/> or <see cref="WikiPagePropertyList{T}.PageId"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override IAsyncEnumerable<WikiPage> EnumPagesAsync(IWikiPageQueryProvider options)
    {
        return base.EnumPagesAsync(options);
    }

}
