using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
using HtmlAgilityPack;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Wikia.Discussions;

namespace WikiClientLibrary.Wikia
{
    internal static class RequestHelper
    {

        public static IAsyncEnumerable<Post> EnumArticleCommentsAsync(ArticleCommentArea articleCommentArea)
        {
            using (articleCommentArea.Site.BeginActionScope(articleCommentArea))
            {
                return AsyncEnumerableFactory.FromAsyncGenerator<Post>(async (sink, ct) =>
                {
                    // Refresh to get the page id.
                    if (articleCommentArea.Id == 0) await articleCommentArea.RefreshAsync(ct);
                    if (!articleCommentArea.Exists) return;
                    var page = 1;
                    while (true)
                    {
                        var doc = await articleCommentArea.Site.InvokeNirvanaAsync(new WikiaQueryRequestMessage(new
                        {
                            format = "html",
                            controller = "ArticleComments",
                            method = "Content",
                            articleId = articleCommentArea.Id,
                            page = page
                        }), WikiaHtmlResponseParser.Default, ct);
                        var rootNode = doc.GetElementbyId("article-comments-ul")
                                       ?? doc.DocumentNode.SelectSingleNode(".//ul[@class='comments']");
                        if (rootNode == null)
                            throw new UnexpectedDataException("Cannot locate comments root node.");
                        await sink.YieldAndWait(Post.FromHtmlCommentsRoot(articleCommentArea.Site, articleCommentArea.Id, rootNode));
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

        public static async Task<Post> PostCommentAsync(WikiaSite site, object scopeInst, int pageId, int? parentId, string content, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            using (site.BeginActionScope(scopeInst, pageId, parentId))
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
