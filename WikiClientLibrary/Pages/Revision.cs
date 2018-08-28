using System;
using System.Collections.Generic;
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
        /// to the lastest revision fetched in this invocation, and pages with the same title
        /// share the same reference.</para>
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="revisionId"/> is not an existing revision id.</exception>
        public static Task<Revision> FetchRevisionAsync(WikiSite site, int revisionId)
        {
            return FetchRevisionsAsync(site, new[] {revisionId}, PageQueryOptions.FetchContent).First();
        }

        /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{int},IWikiPageQueryProvider,CancellationToken)"/>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, params int[] revisionIds)
        {
            return FetchRevisionsAsync(site, revisionIds, PageQueryOptions.FetchContent, CancellationToken.None);
        }

        /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{int},IWikiPageQueryProvider,CancellationToken)"/>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revisionIds)
        {
            return FetchRevisionsAsync(site, revisionIds, PageQueryOptions.FetchContent, CancellationToken.None);
        }

        /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{int},IWikiPageQueryProvider,CancellationToken)"/>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revisionIds, PageQueryOptions options)
        {
            return FetchRevisionsAsync(site, revisionIds, options, CancellationToken.None);
        }

        /// <inheritdoc cref="FetchRevisionsAsync(WikiSite,IEnumerable{int},IWikiPageQueryProvider,CancellationToken)"/>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revisionIds, PageQueryOptions options, CancellationToken cancellationToken)
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
        /// to the lastest revision fetched in this invocation, and pages with the same title
        /// share the same reference.</para>
        /// <para>If there's invalid revision id in <paramref name="revisionIds"/>, an <see cref="ArgumentException"/>
        /// will be thrown while enumerating.</para>
        /// </remarks>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revisionIds, IWikiPageQueryProvider options, CancellationToken cancellationToken)
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
        public int Id { get; private set; }

        [JsonProperty]
        public int ParentId { get; private set; }

        /// <summary>
        /// Gets the content of the revision.
        /// </summary>
        /// <value>Wikitext source code. OR <c>null</c> if content has not been fetched.</value>
        /// <seealso cref="RevisionsPropertyProvider.FetchContent"/>
        [JsonProperty("*")]
        public string Content { get; private set; }

        /// <summary>
        /// Editor's edit summary (editor's comment on revision).
        /// </summary>
        [JsonProperty]
        public string Comment { get; private set; }

        /// <summary>
        /// Content model id of the revision.
        /// </summary>
        [JsonProperty]
        public string ContentModel { get; private set; }

        /// <summary>
        /// SHA-1 (base 16) of the revision.
        /// </summary>
        [JsonProperty]
        public string Sha1 { get; private set; }

        /// <summary>
        /// The user who made the revision.
        /// </summary>
        /// <seealso cref="RevisionFlags.Anonymous"/>
        /// <seealso cref="RevisionHiddenFields.User"/>
        [JsonProperty("user")]
        public string UserName { get; private set; }

        /// <summary>
        /// User id of revision creator.
        /// </summary>
        /// <seealso cref="RevisionFlags.Anonymous"/>
        /// <seealso cref="RevisionHiddenFields.User"/>
        [JsonProperty]
        public int UserId { get; private set; }

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
        public IList<string> Tags { get; private set; }

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
        /// Gets a indicator of whether one or more fields has been hidden.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Help:RevisionDelete .</remarks>
        public RevisionHiddenFields HiddenFields { get; private set; }

#pragma warning disable 649
        [JsonProperty] private bool Minor;
        [JsonProperty] private bool Bot;
        [JsonProperty] private bool New;
        [JsonProperty] private bool Anon;
        [JsonProperty] private bool UserHidden;
#pragma warning restore 649

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
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var tags = Tags == null ? null : MediaWikiHelper.JoinValues(Tags);
            return $"Revision#{Id}, {Flags}, {tags}, SHA1={Sha1}";
        }
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