using System.Collections.Immutable;
using System.Text.Json.Serialization;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages;

/// <summary>
/// Represents a revision of a page.
/// </summary>
/// <seealso cref="RevisionsPropertyGroup"/>
/// <seealso cref="RevisionsPropertyProvider"/>
[JsonContract]
public sealed class Revision
{

    private readonly IDictionary<string, RevisionSlot> slots = ImmutableDictionary<string, RevisionSlot>.Empty;

    /// <summary>
    /// Fetch a revision by revid. This overload will also fetch the content of revision.
    /// </summary>
    /// <remarks>
    /// <para>The <see cref="WikiPage"/> of returned <see cref="Revision"/> will be a valid object.
    /// However, its <see cref="WikiPage.LastRevision"/> and <see cref="WikiPage.Content"/> will corresponds
    /// to the latest revision fetched in this invocation, and pages with the same title
    /// share the same reference.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="revisionId"/> is not an existing revision id.</exception>
    public static ValueTask<Revision> FetchRevisionAsync(WikiSite site, long revisionId)
    {
        return FetchRevisionsAsync(site, new[] { revisionId }, PageQueryOptions.FetchContent).FirstAsync()!;
    }

    /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{long},IWikiPageQueryProvider,CancellationToken)"/>
    public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, params long[] revisionIds)
    {
        return FetchRevisionsAsync(site, revisionIds, PageQueryOptions.FetchContent, CancellationToken.None);
    }

    /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{long},IWikiPageQueryProvider,CancellationToken)"/>
    public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, IEnumerable<long> revisionIds)
    {
        return FetchRevisionsAsync(site, revisionIds, PageQueryOptions.FetchContent, CancellationToken.None);
    }

    /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{long},IWikiPageQueryProvider,CancellationToken)"/>
    public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, IEnumerable<long> revisionIds, PageQueryOptions options)
    {
        return FetchRevisionsAsync(site, revisionIds, options, CancellationToken.None);
    }

    /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{long},IWikiPageQueryProvider,CancellationToken)"/>
    public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, IEnumerable<long> revisionIds, PageQueryOptions options,
        CancellationToken cancellationToken)
    {
        return FetchRevisionsAsync(site, revisionIds, MediaWikiHelper.QueryProviderFromOptions(options), cancellationToken);
    }

    /// <summary>
    /// Fetch revisions by revid sequence.
    /// </summary>
    /// <param name="site">The site to fetch revisions from.</param>
    /// <param name="revisionIds">The desired revision Ids.</param>
    /// <param name="options">The options for fetching the revisions.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="revisionIds"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="revisionIds"/> contains an existing revision id.</exception>
    /// <remarks>
    /// <para>The returned sequence will have the SAME order as specified in <paramref name="revisionIds"/>.</para>
    /// <para>The <see cref="WikiPage"/> of returned <see cref="Revision"/> will be a valid object.
    /// However, its <see cref="WikiPage.LastRevision"/> and <see cref="WikiPage.Content"/> will corresponds
    /// to the latest revision fetched in this invocation, and pages with the same title
    /// share the same reference.</para>
    /// <para>If there's invalid revision id in <paramref name="revisionIds"/>, an <see cref="ArgumentException"/>
    /// will be thrown while enumerating.</para>
    /// </remarks>
    public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, IEnumerable<long> revisionIds,
        IWikiPageQueryProvider options, CancellationToken cancellationToken)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (revisionIds == null) throw new ArgumentNullException(nameof(revisionIds));
        return RequestHelper.FetchRevisionsAsync(site, revisionIds, options, cancellationToken);
    }

    /// <summary>
    /// Gets the stub of page this revision applies to.
    /// </summary>
    public WikiPageStub Page { get; internal set; }

    [JsonPropertyName("revid")]
    public long Id { get; init; }

    public long ParentId { get; init; }

    /// <summary>
    /// Gets the content of the revision.
    /// </summary>
    /// <value>
    /// Wikitext source code. OR <c>null</c> if content has not been fetched.
    /// For MediaWiki version with slot support, this is the content of <c>main</c> slot.
    /// </value>
    /// <remarks>
    /// See <see cref="RevisionSlot"/> for more information on the revision slots.
    /// </remarks>
    /// <seealso cref="RevisionsPropertyProvider.FetchContent"/>
    [JsonPropertyName("*")]
    public string? Content { get; init; }

    /// <summary>
    /// Editor's edit summary (editor's comment on revision).
    /// </summary>
    public string Comment { get; init; } = "";

    /// <summary>
    /// Content model id of the revision.
    /// </summary>
    public string ContentModel { get; init; } = "";

    /// <summary>
    /// SHA-1 (base 16) of the revision.
    /// </summary>
    public string Sha1 { get; init; } = "";

    /// <summary>
    /// The user who made the revision.
    /// </summary>
    /// <seealso cref="RevisionFlags.Anonymous"/>
    /// <seealso cref="RevisionHiddenFields.User"/>
    [JsonPropertyName("user")]
    public string UserName { get; init; } = "";

    /// <summary>
    /// User id of revision creator.
    /// </summary>
    /// <seealso cref="RevisionFlags.Anonymous"/>
    /// <seealso cref="RevisionHiddenFields.User"/>
    public long UserId { get; init; }

    /// <summary>
    /// Gets a <see cref="UserStub"/> containing the name and ID of the user made this revision.
    /// </summary>
    public UserStub UserStub => new UserStub(UserName, UserId);

    /// <summary>
    /// Content length, in bytes.
    /// </summary>
    /// <seealso cref="WikiPage.ContentLength"/>
    [JsonPropertyName("size")]
    public int ContentLength { get; init; }

    /// <summary>
    /// Any tags for this revision, such as those added by <a href="https://www.mediawiki.org/wiki/Extension:AbuseFilter">AbuseFilter</a>.
    /// </summary>
    public IList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The timestamp of revision.
    /// </summary>
    public DateTime TimeStamp { get; init; }

    /// <summary>
    /// Additional status of the revision.
    /// </summary>
    public RevisionFlags Flags { get; init; }

    /// <summary>
    /// Revision slots. (MW 1.32+)
    /// </summary>
    /// <value>Revision slots, or empty dictionary if the site does not support revision slot.</value>
    /// <seealso cref="RevisionSlot"/>
    public IDictionary<string, RevisionSlot> Slots
    {
        get => slots;
        init
        {
            slots = value;
            // Make compatible with the slot-based revision JSON
            if (value.TryGetValue(RevisionSlot.MainSlotName, out var mainSlot))
            {
                if (string.IsNullOrEmpty(Content)) Content = mainSlot.Content;
                if (ContentLength == 0) ContentLength = mainSlot.ContentLength;
                if (string.IsNullOrEmpty(ContentModel)) ContentModel = mainSlot.ContentModel;
                if (string.IsNullOrEmpty(Sha1)) Sha1 = mainSlot.Sha1;
            }
        }
    }

    /// <summary>
    /// Gets an indicator of whether one or more fields has been hidden.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/Help:RevisionDelete .</remarks>
    public RevisionHiddenFields HiddenFields { get; init; }

    [JsonInclude]
    private bool Minor
    {
        init => Flags = value ? (Flags | RevisionFlags.Minor) : (Flags & ~RevisionFlags.Minor);
    }

    [JsonInclude]
    private bool Bot
    {
        init => Flags = value ? (Flags | RevisionFlags.Bot) : (Flags & ~RevisionFlags.Bot);
    }

    [JsonInclude]
    private bool New
    {
        init => Flags = value ? (Flags | RevisionFlags.Create) : (Flags & ~RevisionFlags.Create);
    }

    [JsonInclude]
    private bool Anon
    {
        init => Flags = value ? (Flags | RevisionFlags.Anonymous) : (Flags & ~RevisionFlags.Anonymous);
    }

    [JsonInclude]
    private bool UserHidden
    {
        init => HiddenFields = value ? (HiddenFields | RevisionHiddenFields.User) : (HiddenFields & ~RevisionHiddenFields.User);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var tags = MediaWikiHelper.JoinValues(Tags);
        return $"Revision#{Id}, {Flags}, {tags}, SHA1={Sha1}";
    }

}

/// <summary>
/// Represents a slot in a revision. (MW 1.32+)
/// </summary>
/// <remarks>
/// <para>"Slot"s have been introduced in MediaWiki 1.32 as part of
/// <a href="https://www.mediawiki.org/wiki/Multi-Content_Revisions">Multi-Content Revisions</a>.
/// For more information about revision slots, see
/// <a href="https://www.mediawiki.org/wiki/Manual:Slot">mw:Manual:slot</a>.</para>
/// </remarks>
/// <seealso cref="RevisionsPropertyProvider.Slots"/>
[JsonContract]
public sealed class RevisionSlot
{

    /// <summary>
    /// Revision slot name for main revisions.
    /// </summary>
    public const string MainSlotName = "main";

    /// <summary>
    /// Revision slot name intended for template documentations in future MediaWiki releases.
    /// </summary>
    public const string DocumentationSlotName = "documentation";

    /// <summary>Slot content.</summary>
    /// <value>slot content. This property will be <c>null</c> if the content has not been requested to be fetched.</value>
    [JsonPropertyName("*")]
    public string? Content { get; init; }

    /// <summary>
    /// Content serialization format used for the revision slot. 
    /// </summary>
    /// <remarks>
    /// Possible values: text/x-wiki (wikitext), text/javascript (javascript), text/css (css),
    /// text/plain (plain text), application/json (json).
    /// </remarks>
    public string ContentFormat { get; init; } = "";

    /// <summary>
    /// Content model of the revision slot.
    /// </summary>
    /// <remarks>
    /// Possible values: wikitext, javascript, css, text and json.
    /// This list may include additional values registered by extensions;
    /// on Wikimedia wikis, these include: JsonZeroConfig, Scribunto, JsonSchema
    /// </remarks>
    public string ContentModel { get; init; } = "";

    /// <summary>
    /// Content length (in bytes) of the revision slot.
    /// </summary>
    [JsonPropertyName("size")]
    public int ContentLength { get; init; }

    /// <summary>
    /// SHA-1 (base 16) of the revision slot.
    /// </summary>
    public string Sha1 { get; init; } = "";

}

/// <summary>
/// Revision flags.
/// </summary>
/// <seealso cref="Revision.Flags"/>
/// <seealso cref="RecentChangeItem.Flags"/>
[Flags]
public enum RevisionFlags
{

    None = 0,

    /// <summary>
    /// The revision is a minor edit.
    /// </summary>
    Minor = 1,

    /// <summary>
    /// The operation is performed by bot.
    /// This flag can only be accessed via <see cref="RecentChangeItem.Flags"/>.
    /// </summary>
    Bot = 2,

    /// <summary>
    /// A new page has been created by this revision.
    /// </summary>
    Create = 4,

    /// <summary>
    /// The revision's editor is an anonymous user.
    /// </summary>
    Anonymous = 8,

}

/// <summary>
/// Hidden part of revision indicators.
/// </summary>
/// <remarks>See <a href="https://www.mediawiki.org/wiki/Help:RevisionDelete">mw:Help:RevisionDelete</a>.</remarks>
/// <seealso cref="Revision.HiddenFields"/>
[Flags]
public enum RevisionHiddenFields
{

    /// <summary>
    /// No field has been hidden.
    /// </summary>
    None = 0,

    /// <summary>
    /// The editor's name has been hidden.
    /// </summary>
    User = 1,
    //TODO Content & Comment

}
