using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia.Discussions;

namespace WikiClientLibrary.Wikia
{
    internal static class RequestHelper
    {

        public static IAsyncEnumerable<Post> EnumArticleCommentsAsync(ArticleCommentArea board, PostQueryOptions options)
        {
            IList<Post> PostsFromJsonOutline(JObject commentList)
            {
                return commentList.Properties().Select(p =>
                {
                    var post = new Post(board.Site, board.Id, Convert.ToInt32(p.Name));
                    var level2 = (JObject)p.Value["level2"];
                    if (level2 != null && level2.HasValues)
                    {
                        post.Replies = new ReadOnlyCollection<Post>(level2.Properties().Select(p2 =>
                            new Post(board.Site, board.Id, Convert.ToInt32(p2.Name))).ToList());
                    }
                    return post;
                }).ToList();
            }

            IEnumerable<Post> PostsAndDescendants(IEnumerable<Post> posts)
            {
                foreach (var p in posts)
                {
                    yield return p;
                    if (p.Replies.Count > 0)
                    {
                        foreach (var p2 in p.Replies)
                        {
                            yield return p2;
                            // Wikia only supports level-2 comments for now.
                            Debug.Assert(p2.Replies.Count == 0);
                        }
                    }
                }
            }

            return AsyncEnumerableFactory.FromAsyncGenerator<Post>(async (sink, ct) =>
            {
                using (board.Site.BeginActionScope(board))
                {
                    // Refresh to get the page id.
                    if (board.Id == 0) await board.RefreshAsync(ct);
                    if (!board.Exists) return;
                    var pagesCount = 1;
                    for (int page = 1; page <= pagesCount; page++)
                    {
                        var jroot = await board.Site.InvokeNirvanaAsync(new WikiaQueryRequestMessage(new
                        {
                            format = "json",
                            controller = "ArticleComments",
                            method = "Content",
                            articleId = board.Id,
                            page = page
                        }), WikiaJsonResonseParser.Default, ct);
                        // Build comment structure.
                        var jcomments = (JObject)jroot["commentListRaw"];
                        if (jcomments != null && jcomments.HasValues)
                        {
                            var comments = PostsFromJsonOutline(jcomments);
                            pagesCount = (int)jroot["pagesCount"];
                            await RefreshPostsAsync(PostsAndDescendants(comments), options, ct);
                            await sink.YieldAndWait(comments);
                        }
                    }
                }
            });
        }

        public static async Task RefreshPostsAsync(IEnumerable<Post> posts,
            PostQueryOptions options, CancellationToken cancellationToken)
        {
            Debug.Assert(posts != null);
            // Fetch comment content.
            // You can even fetch posts from different sites.
            foreach (var sitePosts in posts.GroupBy(p => p.Site))
            {
                var site = sitePosts.Key;
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in sitePosts.Partition(titleLimit))
                {
                    using (site.BeginActionScope(partition, options))
                    {
                        // Fetch last revisions to determine content and last editor
                        var lastRevisionTask = site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                        {
                            action = "query",
                            pageids = string.Join("|", partition.Select(p => p.Id)),
                            prop = "revisions",
                            rvlimit = partition.Count == 1 ? (int?)1 : null,
                            rvprop = MediaWikiHelper.GetQueryParamRvProp(PageQueryOptions.FetchContent)
                        }), cancellationToken);
                        // Fetch the first revisions, when needed, to determine author.
                        Dictionary<int, Revision> firstRevisionDict = null;
                        if (partition.Count == 1
                            || (options & PostQueryOptions.ExactAuthoringInformation) == PostQueryOptions.ExactAuthoringInformation)
                        {
                            firstRevisionDict = new Dictionary<int, Revision>();
                            foreach (var post in partition)
                            {
                                // We can only fetch for 1 page at a time, with rvdir = "newer"
                                var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                                {
                                    action = "query",
                                    pageids = post.Id,
                                    prop = "revisions",
                                    rvdir = "newer",
                                    rvlimit = 1,
                                    rvprop = MediaWikiHelper.GetQueryParamRvProp(PageQueryOptions.FetchContent)
                                }), cancellationToken);
                                var jpage = jresult["query"]["pages"][post.Id.ToString(CultureInfo.InvariantCulture)];
                                if (jpage["missing"] != null)
                                {
                                    post.Exists = false;
                                    continue;
                                }
                                var pageStub = MediaWikiHelper.PageStubFromRevision((JObject)jpage);
                                var rev = MediaWikiHelper.RevisionFromJson((JObject)jpage["revisions"].First, pageStub);
                                firstRevisionDict[post.Id] = rev;
                            }

                        }
                        var jLastRevResult = await lastRevisionTask;
                        foreach (var post in partition)
                        {
                            var jpage = jLastRevResult["query"]["pages"][post.Id.ToString(CultureInfo.InvariantCulture)];
                            var pageStub = MediaWikiHelper.PageStubFromRevision((JObject)jpage);
                            var lastRev = MediaWikiHelper.RevisionFromJson((JObject)jpage["revisions"].First, pageStub);
                            Revision firstRev = null;
                            firstRevisionDict?.TryGetValue(post.Id, out firstRev);
                            post.SetRevisions(firstRev, lastRev);
                        }
                    }
                }
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
