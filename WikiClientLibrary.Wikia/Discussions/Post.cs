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

namespace WikiClientLibrary.Wikia.Discussions
{
    /// <summary>
    /// Represents a post in the discussion.
    /// </summary>
    public class Post
    {

        private static readonly Post[] emptyPosts = { };

        public Post(WikiaSite site, int pageId, int id)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            PageId = pageId;
            Id = id;
        }

        public WikiaSite Site { get; }

        /// <summary>Gets the ID of the post.</summary>
        public int Id { get; private set; }

        /// <summary>Gets the ID of the page that owns the comment.</summary>
        public int PageId { get; private set; }

        public bool Exists { get; internal set; }

        /// <summary>Gets the comment content.</summary>
        public string Content { get; set; }

        /// <summary>Gets the author of the post.</summary>
        public UserStub Author { get; private set; }

        /// <summary>Gets the last editor of the post.</summary>
        public UserStub LastEditor { get; private set; }

        public DateTime TimeStamp { get; private set; }

        public DateTime? LastUpdated { get; private set; }

        public Revision LastRevision { get; private set; }

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
            return RequestHelper.RefreshPostsAsync(new[] {this}, options, cancellationToken);
        }

        /// <inheritdoc cref="ReplyAsync(string,CancellationToken)"/>
        public Task<Post> ReplyAsync(string content)
        {
            return ReplyAsync(content, CancellationToken.None);
        }

        /// <summary>
        /// Add a new reply to the post.
        /// </summary>
        /// <param name="content">The content in reply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A new post containing the workflow ID of the new post.</returns>
        /// <remarks>For now Wikia only supports 2-level posts.
        /// If you attempt to reply to the level-2 post,
        /// the comment will be placed as level-2 (top-level) post.</remarks>
        public Task<Post> ReplyAsync(string content, CancellationToken cancellationToken)
        {
            return RequestHelper.PostCommentAsync(Site, this, PageId, Id, content, cancellationToken);
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
            Replies = emptyPosts;
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

        internal static Post FromHtmlNode(WikiaSite site, int pageId, HtmlNode listItem)
        {
            Debug.Assert(listItem.Name == "li");
            int id = 0;
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
                throw new UnexpectedDataException("Unexpected li[@id] value: " + listItem.Id);
            return new Post(site, pageId, id);
        }

    }

    /// <summary>
    /// Provides options for fetching a post from the server.
    /// </summary>
    [Flags]
    public enum PostQueryOptions
    {
        /// <summary>No options.</summary>
        None,
        /// <summary>
        /// Asks for exact author information, even if we are fetching for multiple
        /// comments at one time.
        /// </summary>
        /// <remarks>
        /// <para>Due to the limitations of Wikia API, we need to issue multiple requests to the
        /// server to retrieve the first revisions and the last revisions for the comments.
        /// To reduce network traffic, by default the <see cref="ArticleCommentArea.EnumPostsAsync(PostQueryOptions)"/>
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

}
