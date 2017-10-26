using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WikiClientLibrary.Wikia.Discussions
{
    /// <summary>
    /// Represents a post in the discussion.
    /// </summary>
    public class Post
    {

        private static readonly Post[] emptyPosts = { };

        public int Id { get; private set; }

        public string Content { get; set; }

        public string ParsedContent { get; private set; }

        public string AuthorName { get; private set; }

        public DateTime TimeStamp { get; private set; }

        public IReadOnlyList<Post> Replies { get; private set; } = emptyPosts;

        // http://xxx.wikia.com/wiki/Talk:ArticleName/@comment-UserName-20170704160847?permalink=1234#comm-1234
        private static readonly Regex PermalinkTimeStampMatcher = new Regex(@"@comment-(?<UserName>.+?)-(?<TimeStamp>\d{14})[&?#$]");

        internal static Post FromHtmlNode(HtmlNode listItem)
        {
            if (listItem == null) throw new ArgumentNullException(nameof(listItem));
            var post = new Post();
            post.LoadFromHtmlNode(listItem);
            return post;
        }

        internal void LoadFromHtmlNode(HtmlNode listItem)
        {
            Debug.Assert(listItem.Name == "li");
            Id = 0;
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
                Replies = new ReadOnlyCollection<Post>(FromHtmlCommentsRoot(node).ToList());
            }
        }

        internal static IEnumerable<Post> FromHtmlCommentsRoot(HtmlNode rootNode)
        {
            Debug.Assert(rootNode.Name == "ul");
            var replyNodes = rootNode.SelectNodes("li");
            if (replyNodes != null && replyNodes.Count > 0)
                return replyNodes.Select(FromHtmlNode).ToList();
            return Enumerable.Empty<Post>();
        }

    }
}
