using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
using HtmlAgilityPack;
using WikiClientLibrary.Infrastructures.Logging;

namespace WikiClientLibrary.Wikia.Discussions
{
    public static class DiscussionsExtensions
    {

        /// <summary>
        /// Asynchronously enumerates all the comments on the specified page.
        /// </summary>
        /// <param name="site">The site to issue the request.</param>
        /// <param name="pageId">The page id from which to enumerate the comments</param>
        public static IAsyncEnumerable<Post> EnumArticleCommentsAsync(this WikiaSite site, int pageId)
        {
            using (site.BeginActionScope(pageId))
            {
                return AsyncEnumerableFactory.FromAsyncGenerator<Post>(async (sink, ct) =>
                {
                    var page = 1;
                    while (true)
                    {
                        var doc = await site.InvokeNirvanaAsync(new WikiaQueryRequestMessage(new
                        {
                            format = "html",
                            controller = "ArticleComments",
                            method = "Content",
                            articleId = pageId,
                            page = page
                        }), WikiaHtmlResponseParser.Default, ct);
                        var rootNode = doc.GetElementbyId("article-comments-ul")
                                       ?? doc.DocumentNode.SelectSingleNode(".//ul[@class='comments']");
                        if (rootNode == null)
                            throw new UnexpectedDataException("Cannot locate comments root node.");
                        await sink.YieldAndWait(Post.FromHtmlCommentsRoot(site, pageId, rootNode));
                        var paginationNode = doc.GetElementbyId("article-comments-pagination-link-next");
                        var nextPageExpr = paginationNode?.GetAttributeValue("page", "");
                        if (string.IsNullOrEmpty(nextPageExpr)) return;
                        var nextPageNumber = Convert.ToInt32(nextPageExpr);
                        Debug.Assert(nextPageNumber > page, "The next page number should increase.");
                        page = nextPageNumber;
                    }
                });
            }
        }

        /// <inheritdoc cref="PostCommentAsync(WikiaSite,int,int?,string,CancellationToken)"/>
        public static Task<Post> PostCommentAsync(this WikiaSite site, int pageId, int? parentId, string content)
        {
            return PostCommentAsync(site, pageId, parentId, content, CancellationToken.None);
        }

        /// <summary>
        /// Add a new reply to the post.
        /// </summary>
        /// <param name="site">The site to issue the request.</param>
        /// <param name="pageId">The page ID to post new comment.</param>
        /// <param name="parentId">The parent comment ID to reply.</param>
        /// <param name="content">The content in reply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A new post containing the workflow ID of the new post.</returns>
        public static async Task<Post> PostCommentAsync(this WikiaSite site, int pageId, int? parentId, string content, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            using (site.BeginActionScope(null, pageId, parentId))
            {
                var jresult = await site.InvokeWikiaAjaxAsync(new WikiaQueryRequestMessage(new
                {
                    article = pageId,
                    rs = "ArticleCommentsAjax",
                    method = "axPost",
                    token = await site.GetTokenAsync("edit", cancellationToken),
                    convertToFormat = "",
                    parentId = parentId,
                    wpArticleComment = content,
                }, true), WikiaJsonResonseParser.Default, cancellationToken);
                if (((int?)jresult["error"] ?? 0) != 0)
                    throw new OperationFailedException((string)jresult["msg"]);
                var text = (string)jresult["text"];
                var doc = new HtmlDocument();
                doc.LoadHtml(text);
                var node = doc.DocumentNode.SelectSingleNode("li");
                if (node == null)
                    throw new UnexpectedDataException("Cannot locate the text node in the Wikia API response.");
                return Post.FromHtmlNode(site, pageId, node);
            }
        }
    }
}
