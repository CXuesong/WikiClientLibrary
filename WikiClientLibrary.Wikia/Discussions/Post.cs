using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia.Sites;

namespace WikiClientLibrary.Wikia.Discussions
{
    /// <summary>
    /// Represents a post in the commenting area,
    /// or a thread in a board on the Wikia forum (<c>Special:Forum</c>).
    /// </summary>
    public class Post
    {

        private static readonly Post[] emptyPosts = { };

        /// <summary>
        /// Initializes a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="site">The Wikia site.</param>
        /// <param name="ownerPage">Stub of the page/board that owns the post.</param>
        /// <param name="id">ID of the post.</param>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="ownerPage"/>.<see cref="WikiPageStub.IsEmpty"/> is <c>true</c>.</exception>
        /// <remarks>
        /// For now, it is not possible to get the replies by using the constructor provided here.
        /// To achieve this, you need to invoke <see cref="Board.EnumPostsAsync()"/> on a <see cref="Board"/> instance.
        /// </remarks>
        public Post(WikiaSite site, WikiPageStub ownerPage, int id)
        {
            if (ownerPage.IsEmpty) throw new ArgumentException("ownerPage is empty.", nameof(ownerPage));
            Site = site ?? throw new ArgumentNullException(nameof(site));
            OwnerPage = ownerPage;
            Id = id;
        }

        /// <summary>Gets the Wikia site.</summary>
        public WikiaSite Site { get; }

        /// <summary>Gets the ID of the post.</summary>
        public int Id { get; private set; }

        /// <summary>Gets the stub of the page that owns the comment.</summary>
        public WikiPageStub OwnerPage { get; private set; }

        /// <summary>Gets a value, determining whether the page exists.</summary>
        /// <remarks>If you have created the <see cref="Post"/> instance via its constructor,
        /// this value is valid only after a <see cref="RefreshAsync()"/> call.</remarks>
        public bool Exists { get; internal set; }

        /// <summary>Gets the comment content.</summary>
        public string Content { get; set; }

        /// <summary>Gets the author of the post.</summary>
        public UserStub Author { get; private set; }

        /// <summary>Gets the last editor of the post.</summary>
        public UserStub LastEditor { get; private set; }

        /// <summary>Gets the time when the post is first submitted.</summary>
        public DateTime TimeStamp { get; private set; }

        /// <summary>Gets the time when the post is last edited.</summary>
        /// <value>The time when the post is last edited, or <c>null</c>, if the post has not been edited at all.</value>
        public DateTime? LastUpdated { get; private set; }

        /// <summary>Gets the latest revision of the post.</summary>
        public Revision LastRevision { get; private set; }

        /// <summary>Gets the replies of the post.</summary>
        public IReadOnlyList<Post> Replies { get; internal set; } = emptyPosts;

        /// <inheritdoc cref="RefreshAsync(PostQueryOptions,CancellationToken)"/>
        /// <seealso cref="DiscussionsExtensions.RefreshAsync(IEnumerable{Post})"/>
        public Task RefreshAsync()
        {
            return RefreshAsync(PostQueryOptions.None, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(PostQueryOptions,CancellationToken)"/>
        /// <seealso cref="DiscussionsExtensions.RefreshAsync(IEnumerable{Post},PostQueryOptions)"/>
        public Task RefreshAsync(PostQueryOptions options)
        {
            return RefreshAsync(options, CancellationToken.None);
        }

        /// <summary>
        /// Refreshes the post content from the server.
        /// </summary>
        /// <param name="options">The options used to fetch the post.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <remarks>
        /// This method will not fetch replies. <see cref="Replies"/> will remain unchanged after the invocation.
        /// </remarks>
        /// <seealso cref="DiscussionsExtensions.RefreshAsync(IEnumerable{Post},PostQueryOptions,CancellationToken)"/>
        public Task RefreshAsync(PostQueryOptions options, CancellationToken cancellationToken)
        {
            return RequestHelper.RefreshPostsAsync(new[] { this }, options, cancellationToken);
        }

        internal const int METHOD_UNKNOWN = -1;
        internal const int METHOD_ARTICLE_COMMENT = 0;
        internal const int METHOD_WALL_MESSAGE = 1;

        internal static int GetPostCreationMethod(WikiPageStub owner, PostCreationOptions options)
        {
            if ((options & PostCreationOptions.AsArticleComment) == PostCreationOptions.AsArticleComment
                && (options & PostCreationOptions.AsWallMessage) == PostCreationOptions.AsWallMessage)
                throw new ArgumentException("AsArticleComment and AsWallMessage are mutually exclusive.", nameof(options));
            if ((options & PostCreationOptions.AsArticleComment) == PostCreationOptions.AsArticleComment)
                return METHOD_ARTICLE_COMMENT;
            if ((options & PostCreationOptions.AsWallMessage) == PostCreationOptions.AsWallMessage)
                return METHOD_WALL_MESSAGE;
            if (!owner.HasNamespaceId) return METHOD_UNKNOWN;
            var ns = owner.NamespaceId;
            return (ns == WikiaNamespaces.MessageWall || ns == WikiaNamespaces.Board)
                ? METHOD_WALL_MESSAGE
                : METHOD_ARTICLE_COMMENT;
        }

        /// <inheritdoc cref="ReplyAsync(string,PostCreationOptions,CancellationToken)"/>
        public Task<Post> ReplyAsync(string content)
        {
            return ReplyAsync(content, PostCreationOptions.None, CancellationToken.None);
        }

        /// <inheritdoc cref="ReplyAsync(string,PostCreationOptions,CancellationToken)"/>
        public Task<Post> ReplyAsync(string content, PostCreationOptions options)
        {
            return ReplyAsync(content, options, CancellationToken.None);
        }

        /// <summary>
        /// Add a new reply to the post.
        /// </summary>
        /// <param name="content">The content in reply.</param>
        /// <param name="options">The options for creating the reply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A new post containing the workflow ID of the new post.</returns>
        /// <remarks>For now Wikia only supports 2-level posts.
        /// If you attempt to reply to the level-2 post,
        /// the comment will be placed as level-2 (top-level) post.</remarks>
        public async Task<Post> ReplyAsync(string content, PostCreationOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var method = GetPostCreationMethod(OwnerPage, options);
            if (method == METHOD_UNKNOWN)
            {
                await RefreshAsync(PostQueryOptions.None, cancellationToken);
                method = GetPostCreationMethod(OwnerPage, options);
                Debug.Assert(method != METHOD_UNKNOWN);
            }
            switch (method)
            {
                case METHOD_ARTICLE_COMMENT:
                    if (!OwnerPage.HasId)
                    {
                        await RefreshAsync(PostQueryOptions.None, cancellationToken);
                        Debug.Assert(OwnerPage.IsMissing || OwnerPage.HasId);
                    }
                    return await RequestHelper.PostCommentAsync(Site, this, OwnerPage, Id, content, cancellationToken);
                case METHOD_WALL_MESSAGE:
                    if (!OwnerPage.HasTitle)
                    {
                        await RefreshAsync(PostQueryOptions.None, cancellationToken);
                        Debug.Assert(OwnerPage.IsMissing || OwnerPage.HasTitle);
                    }
                    return await RequestHelper.ReplyWallMessageAsync(Site, this, OwnerPage, Id, content, cancellationToken);
            }
            return null;
        }

        // Talk:ArticleName/@comment-UserName-20170704160847
        private static readonly Regex PermalinkTimeStampMatcher = new Regex(@"@comment-(?<UserName>.+?)-(?<TimeStamp>\d{14})$");

        internal void SetRevisions(Revision firstRevision, Revision lastRevision)
        {
            Debug.Assert(lastRevision != null);
            if (firstRevision != null)
                Debug.Assert(lastRevision.TimeStamp >= firstRevision.TimeStamp);
            Id = lastRevision.Page.Id;
            Exists = true;
            TimeStamp = DateTime.MinValue;
            Content = lastRevision.Content;
            if (firstRevision == null)
            {
                // Need to infer from the page title
                // Assuming the author hasn't changed the user name
                var match = PermalinkTimeStampMatcher.Match(lastRevision.Page.Title);
                if (match.Success)
                {
                    Author = new UserStub(match.Groups["UserName"].Value, 0);
                    TimeStamp = DateTime.ParseExact(match.Groups["TimeStamp"].Value,
                        "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    if (TimeStamp != lastRevision.TimeStamp)
                        LastUpdated = lastRevision.TimeStamp;
                    else
                        LastUpdated = null;
                }
                else
                {
                    Site.Logger.LogWarning("Cannot infer author from comment page title: {PageStub}.", lastRevision.Page);
                }
            }
            else
            {
                Author = firstRevision.UserStub;
                TimeStamp = firstRevision.TimeStamp;
                if (lastRevision.Id != firstRevision.Id)
                    LastUpdated = lastRevision.TimeStamp;
                else
                    LastUpdated = null;
            }
            LastEditor = lastRevision.UserStub;
            LastRevision = lastRevision;
        }

        internal static Post FromHtmlNode(WikiaSite site, WikiPageStub owner, HtmlNode listItem)
        {
            Debug.Assert(listItem.Name == "li");
            var id = listItem.GetAttributeValue("data-id", 0);
            if (id > 0)
            {
            }
            if (listItem.Id.StartsWith("comm-"))
            {
                id = Convert.ToInt32(listItem.Id.Substring(5));
            }
            else
            {
                var sep = listItem.Id.LastIndexOf('-');
                if (sep >= 0)
                    id = Convert.ToInt32(listItem.Id.Substring(sep + 1));
            }
            if (id == 0)
                throw new UnexpectedDataException($"Cannot infer comment ID from <li> node. @id={listItem.Id}.");
            return new Post(site, owner, id);
        }

    }

    /// <summary>
    /// Provides options for fetching a post from the server.
    /// </summary>
    [Flags]
    public enum PostQueryOptions
    {
        /// <summary>No options.</summary>
        None = 0,
        /// <summary>
        /// Asks for exact author information, even if we are fetching for multiple
        /// comments at one time.
        /// </summary>
        /// <remarks>
        /// <para>Due to the limitations of Wikia API, we need to issue multiple requests to the
        /// server to retrieve the first revisions and the last revisions for the comments.
        /// To reduce network traffic, by default the <see cref="Board.EnumPostsAsync(PostQueryOptions)"/>
        /// will fetch only the latest revision information, and try to determine the author,
        /// as well as the creation time of the comment by parsing the comment page title (e.g. <c>Talk:ArticleName/@comment-UserName-20170704160847</c>).
        /// This is inaccurate, because a user can change his/her user name, while the page title
        /// will not be changed necessarily. If you specify this flag, all the comment's first revision
        /// will be fetched to determine the information of authoring user and time stamp. However,
        /// you should note that MediaWiki API only allows to request for one page at a time when
        /// you are requesting for the earliest revision of a page, so specifying this flag will cause
        /// much network traffic.</para>
        /// <para>If you are fetching for only 1 comment at a time, the exact authoring information
        /// will be fetching regardless of this flag.</para>
        /// </remarks>
        ExactAuthoringInformation
    }

    /// <summary>
    /// Provides options for creating or repying to a post.
    /// </summary>
    [Flags]
    public enum PostCreationOptions
    {
        /// <summary>
        /// No options.
        /// </summary>
        None = 0,
        /// <summary>
        /// Creates the new post using article comment API,
        /// regardless of the current namespace.
        /// </summary>
        AsArticleComment = 1,
        /// <summary>
        /// Creates the new post using message wall API,
        /// regardless of the current namespace.
        /// </summary>
        AsWallMessage = 2,
    }

}
