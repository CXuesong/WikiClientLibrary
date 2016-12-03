using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a revision of a page.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Revision
    {
        internal Revision(Page page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            Page = page;
        }

        /// <summary>
        /// Fetch a revision by revid. This overload will also fetch the content of revision.
        /// </summary>
        /// <remarks>
        /// <para>The <see cref="Page"/> of returned <see cref="Revision"/> will be a valid object.
        /// However, its <see cref="Page.LastRevision"/> and <see cref="Page.Text"/> will corresponds
        /// to the lastest revision fetched in this invocation, and pages with the same title
        /// share the same reference.</para>
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="revisionId"/> is not an existing revision id.</exception>
        public static Task<Revision> FetchRevisionAsync(Site site, int revisionId)
        {
            return FetchRevisionsAsync(site, new[] {revisionId}, PageQueryOptions.FetchContent).First();
        }

        /// <summary>
        /// Fetch revisions by revid sequence. This overload will also fetch the content of revisions.
        /// </summary>
        /// <remarks>
        /// <para>The returned sequence will have the SAME order as specified in <paramref name="revisionIds"/>.</para>
        /// <para>The <see cref="Page"/> of returned <see cref="Revision"/> will be a valid object.
        /// However, its <see cref="Page.LastRevision"/> and <see cref="Page.Text"/> will corresponds
        /// to the lastest revision fetched in this invocation, and pages with the same title
        /// share the same reference.</para>
        /// <para>If there's invalid revision id in <paramref name="revIds"/>, an <see cref="ArgumentException"/>
        /// will be thrown while enumerating.</para>
        /// </remarks>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(Site site, params int[] revisionIds)
        {
            return FetchRevisionsAsync(site, revisionIds, PageQueryOptions.FetchContent);
        }

        /// <summary>
        /// Fetch revisions by revid sequence. This overload will also fetch the content of revisions.
        /// </summary>
        /// <remarks>
        /// <para>The returned sequence will have the SAME order as specified in <paramref name="revisionIds"/>.</para>
        /// <para>The <see cref="Page"/> of returned <see cref="Revision"/> will be a valid object.
        /// However, its <see cref="Page.LastRevision"/> and <see cref="Page.Text"/> will corresponds
        /// to the lastest revision fetched in this invocation, and pages with the same title
        /// share the same reference.</para>
        /// <para>If there's invalid revision id in <paramref name="revIds"/>, an <see cref="ArgumentException"/>
        /// will be thrown while enumerating.</para>
        /// </remarks>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(Site site, IEnumerable<int> revisionIds)
        {
            return FetchRevisionsAsync(site, revisionIds, PageQueryOptions.FetchContent);
        }

        /// <summary>
        /// Fetch revisions by revid sequence.
        /// </summary>
        /// <remarks>
        /// <para>The returned sequence will have the SAME order as specified in <paramref name="revisionIds"/>.</para>
        /// <para>The <see cref="Page"/> of returned <see cref="Revision"/> will be a valid object.
        /// However, its <see cref="Page.LastRevision"/> and <see cref="Page.Text"/> will corresponds
        /// to the lastest revision fetched in this invocation, and pages with the same title
        /// share the same reference.</para>
        /// <para>If there's invalid revision id in <paramref name="revIds"/>, an <see cref="ArgumentException"/>
        /// will be thrown while enumerating.</para>
        /// </remarks>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(Site site, IEnumerable<int> revisionIds, PageQueryOptions options)
        {
            return RequestManager.FetchRevisionsAsync(site, revisionIds, options);
        }

        /// <summary>
        /// Gets the page this revision applies to.
        /// </summary>
        public Page Page { get; private set; }

        [JsonProperty("revid")]
        public int Id { get; private set; }

        [JsonProperty]
        public int ParentId { get; private set; }

        /// <summary>
        /// Gets the content of the revision.
        /// </summary>
        /// <value>Wikitext source code. OR <c>null</c> if content has not been fetched.</value>
        [JsonProperty("*")]
        public string Content { get; private set; }

        [JsonProperty]
        public string Comment { get; private set; }

        [JsonProperty]
        public string ContentModel { get; private set; }

        [JsonProperty]
        public string Sha1 { get; private set; }

        [JsonProperty("user")]
        public string UserName { get; private set; }

        [JsonProperty("size")]
        public int ContentLength { get; private set; }

        [JsonProperty]
        public IList<string> Tags { get; private set; }

        /// <summary>
        /// The timestamp of revision.
        /// </summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

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

        /// <summary>
        /// 返回该实例的完全限定类型名。
        /// </summary>
        /// <returns>
        /// 包含完全限定类型名的 <see cref="T:System.String"/>。
        /// </returns>
        public override string ToString()
        {
            var tags = Tags == null ? null : string.Join("|", Tags);
            return $"Revision#{Id}, {Flags}, {tags}, SHA1={Sha1}";
        }
    }

    /// <summary>
    /// Revision flags.
    /// </summary>
    [Flags]
    public enum RevisionFlags
    {
        None = 0,
        Minor = 1,

        /// <summary>
        /// The operation is performed by bot.
        /// This flag can only be access via <see cref="RecentChangesEntry.Flags"/>.
        /// </summary>
        Bot = 2,
        Create = 4,
        Anonymous = 8,
    }

    /// <summary>
    /// Hidden part of revision indicators.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/Help:RevisionDelete .</remarks>
    [Flags]
    public enum RevisionHiddenFields
    {
        None = 0,
        User = 1,
        //TODO Content & Comment
    }
}