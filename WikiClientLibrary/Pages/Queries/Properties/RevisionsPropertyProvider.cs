using System.Text.Json.Nodes;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties;

/// <summary>
/// Returns the latest revision of the page.
/// (<a href="https://www.mediawiki.org/wiki/API:Revisions">mw:API:Revisions</a>, MediaWiki 1.8+)
/// </summary>
/// <remarks>
/// The <c>prop=revisions</c> module has been implemented as
/// <see cref="RevisionsPropertyProvider"/> and <see cref="RevisionsGenerator"/>.
/// The former allows you to fetch for the latest revisions for multiple pages,
/// while the latter allows you to enumerate the revisions of a single page.
/// </remarks>
public class RevisionsPropertyProvider : WikiPagePropertyProvider<RevisionsPropertyGroup>
{

    /// <summary>
    /// Gets/sets a value that determines whether to fetch revision content.
    /// If set, the maximum limit per API request will be 10 times as low.
    /// (Note: If you want HTML rather than wikitext, use action=parse instead.)
    /// </summary>
    public bool FetchContent { get; set; }

    /// <summary>
    /// Gets/sets the names of the revision slot from which to retrieve the revisions. (MediaWiki 1.32+)
    /// </summary>
    /// <value>
    /// a sequence of slot names, or <c>null</c> to use default slot names (<c>["main"]</c>).
    /// Some example of predefined slot names are <see cref="RevisionSlot.MainSlotName"/> and <see cref="RevisionSlot.DocumentationSlotName"/>.
    /// </value>
    /// <remarks>
    /// <para>See <see cref="RevisionSlot"/> for more information on "slot"s.</para>
    /// </remarks>
    public IEnumerable<string>? Slots { get; set; }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
        var p = new OrderedKeyValuePairs<string, object?>
        {
            {
                "rvprop", FetchContent
                    ? "ids|timestamp|flags|comment|user|userid|contentmodel|sha1|tags|size|content"
                    : "ids|timestamp|flags|comment|user|userid|contentmodel|sha1|tags|size"
            },
        };
        if (Slots != null || version.Above(1, 32))
        {
            // If user specified Slots explicitly, then we will respect it regardless of MW version.
            p.Add("rvslots", Slots == null ? RevisionSlot.MainSlotName : MediaWikiHelper.JoinValues(Slots));
        }
        return p;
    }

    /// <inheritdoc />
    public override int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits) =>
        base.GetMaxPaginationSize(version, apiHighLimits) / 10;

    /// <inheritdoc />
    public override string? PropertyName => "revisions";

    /// <inheritdoc />
    public override RevisionsPropertyGroup? ParsePropertyGroup(JsonObject json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return RevisionsPropertyGroup.Create(json);
    }

}

public class RevisionsPropertyGroup : WikiPagePropertyGroup
{

    private static readonly RevisionsPropertyGroup Empty = new ();

    private Revision[] _Revisions;

    internal static RevisionsPropertyGroup? Create(JsonObject jpage)
    {
        var jrevisions = jpage["revisions"]?.AsArray();
        if (jrevisions == null || jrevisions.Count == 0) return Empty;
        var stub = MediaWikiHelper.PageStubFromJson(jpage);
        return new RevisionsPropertyGroup(stub, jrevisions);
    }

    private RevisionsPropertyGroup()
    {
        _Revisions = Array.Empty<Revision>();
    }

    private RevisionsPropertyGroup(WikiPageStub page, JsonArray jrevisions)
    {
        _Revisions = jrevisions
            .Select(jr => MediaWikiHelper.RevisionFromJson(jr!.AsObject(), page))
            .ToArray();
        Revisions = Array.AsReadOnly(_Revisions);
    }

    public IReadOnlyCollection<Revision> Revisions { get; }

    public Revision? LatestRevision
    {
        get
        {
            var revs = _Revisions;
            if (revs.Length == 0) return null;
            return revs[0].TimeStamp >= revs[^1].TimeStamp ? revs[0] : revs[^1];
        }
    }

}
