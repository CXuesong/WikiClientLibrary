using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Represents a revision of a page.
    /// </summary>
    /// <seealso cref="RevisionsPropertyGroup"/>
    /// <seealso cref="RevisionsPropertyProvider"/>
    [JsonObject(MemberSerialization.OptIn)]
    public class Revision
    {

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
        public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, IEnumerable<long> revisionIds, PageQueryOptions options, CancellationToken cancellationToken)
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
        public static IAsyncEnumerable<Revision?> FetchRevisionsAsync(WikiSite site, IEnumerable<long> revisionIds, IWikiPageQueryProvider options, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (revisionIds == null) throw new ArgumentNullException(nameof(revisionIds));
            return RequestHelper.FetchRevisionsAsync(site, revisionIds, options, cancellationToken);
        }

        /// <summary>
        /// Gets the stub of page this revision applies to.
        /// </summary>
        public WikiPageStub Page { get; internal set; }

        [JsonProperty("revid")]
        public long Id { get; private set; }

        [JsonProperty]
        public long ParentId { get; private set; }

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
        [JsonProperty("*")]
        public string? Content { get; private set; }

        /// <summary>
        /// Editor's edit summary (editor's comment on revision).
        /// </summary>
        [JsonProperty]
        public string Comment { get; private set; } = "";

        /// <summary>
        /// Content model id of the revision.
        /// </summary>
        [JsonProperty]
        public string ContentModel { get; private set; } = "";

        /// <summary>
        /// SHA-1 (base 16) of the revision.
        /// </summary>
        [JsonProperty]
        public string Sha1 { get; private set; } = "";

        /// <summary>
        /// The user who made the revision.
        /// </summary>
        /// <seealso cref="RevisionFlags.Anonymous"/>
        /// <seealso cref="RevisionHiddenFields.User"/>
        [JsonProperty("user")]
        public string UserName { get; private set; } = "";

        /// <summary>
        /// User id of revision creator.
        /// </summary>
        /// <seealso cref="RevisionFlags.Anonymous"/>
        /// <seealso cref="RevisionHiddenFields.User"/>
        [JsonProperty]
        public long UserId { get; private set; }

        /// <summary>
        /// Gets a <see cref="UserStub"/> containing the name and ID of the user made this revision.
        /// </summary>
        [JsonProperty]
        public UserStub UserStub => new UserStub(UserName, UserId);

        /// <summary>
        /// Content length, in bytes.
        /// </summary>
        /// <seealso cref="WikiPage.ContentLength"/>
        [JsonProperty("size")]
        public int ContentLength { get; private set; }

        /// <summary>
        /// Any tags for this revision, such as those added by <a href="https://www.mediawiki.org/wiki/Extension:AbuseFilter">AbuseFilter</a>.
        /// </summary>
        [JsonProperty]
        public IList<string> Tags { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// The timestamp of revision.
        /// </summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// Additional status of the revision.
        /// </summary>
        public RevisionFlags Flags { get; private set; }

        /// <summary>
        /// Revision slots. (MW 1.32+)
        /// </summary>
        /// <value>Revision slots, or empty dictionary if the site does not support revision slot.</value>
        /// <seealso cref="RevisionSlot"/>
        [JsonProperty]
        public IDictionary<string, RevisionSlot> Slots { get; private set; } = ImmutableDictionary<string, RevisionSlot>.Empty;

        /// <summary>
        /// Gets a indicator of whether one or more fields has been hidden.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Help:RevisionDelete .</remarks>
        public RevisionHiddenFields HiddenFields { get; private set; }

#pragma warning disable IDE0044, CS0649 // Add readonly modifier
        [JsonProperty] private bool Minor;
        [JsonProperty] private bool Bot;
        [JsonProperty] private bool New;
        [JsonProperty] private bool Anon;
        [JsonProperty] private bool UserHidden;
#pragma warning restore IDE0044, CS0649 // Add readonly modifier

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Flags = RevisionFlags.None;
            if (Minor) Flags |= RevisionFlags.Minor;
            if (Bot) Flags |= RevisionFlags.Bot;
            if (New) Flags |= RevisionFlags.Create;
            if (Anon) Flags |= RevisionFlags.Anonymous;
            HiddenFields = RevisionHiddenFields.None;
            if (UserHidden) HiddenFields |= RevisionHiddenFields.User;
            // Make compatible with the slot-based revision JSON
            if (Slots.TryGetValue(RevisionSlot.MainSlotName, out var mainSlot))
            {
                if (string.IsNullOrEmpty(Content)) Content = mainSlot.Content;
                if (ContentLength == 0) ContentLength = mainSlot.ContentLength;
                if (string.IsNullOrEmpty(ContentModel)) ContentModel = mainSlot.ContentModel;
                if (string.IsNullOrEmpty(Sha1)) Sha1 = mainSlot.Sha1;
            }
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
    [JsonObject(MemberSerialization.OptIn)]
    public class RevisionSlot
    {

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public RevisionSlot()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

        /// <summary>
        /// Revision slot name for main revisions.
        /// </summary>
        public const string MainSlotName = "main";

        /// <summary>
        /// Revision slot name intended for template documentations in future MediaWiki releases.
        /// </summary>
        public const string DocumentationSlotName = "documentation";

        [JsonProperty("*")]
        public string Content { get; set; }

        /// <summary>
        /// Content serialization format used for the revision slot. 
        /// </summary>
        /// <remarks>
        /// Possible values: text/x-wiki (wikitext), text/javascript (javascript), text/css (css),
        /// text/plain (plain text), application/json (json).
        /// </remarks>
        [JsonProperty]
        public string ContentFormat { get; set; }

        /// <summary>
        /// Content model of the revision slot.
        /// </summary>
        /// <remarks>
        /// Possible values: wikitext, javascript, css, text and json.
        /// This list may include additional values registered by extensions;
        /// on Wikimedia wikis, these include: JsonZeroConfig, Scribunto, JsonSchema
        /// </remarks>
        [JsonProperty]
        public string ContentModel { get; set; }

        /// <summary>
        /// Content length (in bytes) of the revision slot.
        /// </summary>
        [JsonProperty("size")]
        public int ContentLength { get; set; }

        /// <summary>
        /// SHA-1 (base 16) of the revision slot.
        /// </summary>
        [JsonProperty]
        public string Sha1 { get; set; }

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
}