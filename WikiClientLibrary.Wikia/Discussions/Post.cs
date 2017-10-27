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

        /// <summary>Gets the parsed HTML comment content.</summary>
        public string ParsedContent { get; private set; }

        /// <summary>Gets the name of the author.</summary>
        public string AuthorName { get; private set; }

        public DateTime TimeStamp { get; private set; }

        public IReadOnlyList<Post> Replies { get; private set; } = emptyPosts;

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

        // http://xxx.wikia.com/wiki/Talk:ArticleName/@comment-UserName-20170704160847?permalink=1234#comm-1234
        private static readonly Regex PermalinkTimeStampMatcher = new Regex(@"@comment-(?<UserName>.+?)-(?<TimeStamp>\d{14})[&?#$]");

        internal static Post FromHtmlNode(WikiaSite site, int pageId, HtmlNode listItem)
        {
            if (listItem == null) throw new ArgumentNullException(nameof(listItem));
            var post = new Post(site, pageId, 0);
            post.LoadFromHtmlNode(pageId, listItem);
            return post;
        }

        internal void LoadFromHtmlNode(int pageId, HtmlNode listItem)
        {
            Debug.Assert(listItem.Name == "li");
            Id = 0;
            PageId = pageId;
            AuthorName = null;
            TimeStamp = DateTime.MinValue;
            Replies = emptyPosts;
            if (listItem.Id.StartsWith("comm-"))
            {
                Id = Convert.ToInt32(listItem.Id.Substring(5));
            }
            else
            {
                var sep = listItem.Id.LastIndexOf('-');
                if (sep >= 0)
                    Id = Convert.ToInt32(listItem.Id.Substring(sep + 1));
            }
            if (Id == 0)
                throw new UnexpectedDataException("Unexpected li[@id] value: " + listItem.Id);
            var node = listItem.OwnerDocument.GetElementbyId("comm-text-" + Id)
                              ?? listItem.SelectSingleNode(".//*[contains(@class, 'article-comm-text')]");
            if (node == null)
                throw new UnexpectedDataException("Cannot locate content node for comment #" + Id + ".");
            ParsedContent = node.InnerHtml;
            // TODO figure out the wikitext somehow
            Content = node.InnerText;
            node = listItem.SelectSingleNode(".//*[contains(@class, 'edited-by')][not(ancestor::*[contains(@class, 'article-comm-text')])]");
            if (node != null)
            {
                var permalinkNode = node.SelectSingleNode("a[contains(@class, 'permalink')]");
                var permalink = permalinkNode.GetAttributeValue("href", "");
                var matches = PermalinkTimeStampMatcher.Match(permalink);
                if (matches.Success)
                {
                    AuthorName = Uri.UnescapeDataString(matches.Groups["UserName"].Value);
                    TimeStamp = DateTime.ParseExact(matches.Groups["TimeStamp"].Value,
                        "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                }
            }
            node = listItem.NextSibling;
            if (node.Name == "ul" && node.GetAttributeValue("class", "") == "sub-comments")
            {
                Replies = new ReadOnlyCollection<Post>(FromHtmlCommentsRoot(Site, PageId, node).ToList());
            }
        }

        internal static IEnumerable<Post> FromHtmlCommentsRoot(WikiaSite site, int pageId, HtmlNode rootNode)
        {
            Debug.Assert(rootNode.Name == "ul");
            var replyNodes = rootNode.SelectNodes("li");
            if (replyNodes != null && replyNodes.Count > 0)
                return replyNodes.Select(node => FromHtmlNode(site, pageId, node)).ToList();
            return Enumerable.Empty<Post>();
        }

    }
}
