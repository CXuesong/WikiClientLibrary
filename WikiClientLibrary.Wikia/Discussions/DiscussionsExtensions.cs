using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using AsyncEnumerableExtensions;
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
                        await sink.YieldAndWait(Post.FromHtmlCommentsRoot(rootNode));
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


    }
}
