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

        /// <inheritdoc cref="RefreshAsync(CancellationToken)"/>
        public Task RefreshAsync()
        {
            return RefreshAsync(CancellationToken.None);
        }

        /// <summary>
        /// Refreshes the post content from the server.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <remarks>
        /// This method will not fetch replies. <see cref="Replies"/> will remain unchanged after the invocation.
        /// </remarks>
        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            return RequestHelper.RefreshPostsAsync(new[] {this}, cancellationToken);
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

        // Talk:ArticleName/@comment-UserName-20170704160847?permalink=1234#comm-1234
        private static readonly Regex PermalinkTimeStampMatcher = new Regex(@"@comment-(?<UserName>.+?)-(?<TimeStamp>\d{14})$");

        internal void SetRevisions(Revision firstRevision, Revision lastRevision)
        {
            Debug.Assert(lastRevision != null);
            if (firstRevision != null)
                Debug.Assert(lastRevision.TimeStamp >= firstRevision.TimeStamp);
            //Id = lastRevision.Page.Id;
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
                    throw new UnexpectedDataException($"Cannot infer author from comment page title: {lastRevision.Page}.");
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
}
