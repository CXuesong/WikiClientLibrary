using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia.Discussions;
using WikiClientLibrary.Wikia.Sites;

namespace WikiClientLibrary.Wikia;

internal static class RequestHelper
{

    public static async IAsyncEnumerable<Post> EnumArticleCommentsAsync(Board board, PostQueryOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IList<Post> PostsFromJsonOutline(JsonObject commentList)
        {
            return commentList.Select(p =>
            {
                var post = new Post(board.Site, board.Page, Convert.ToInt32(p.Key));
                var level2 = p.Value["level2"]?.AsObject();
                if (level2 != null && level2.Count > 0)
                {
                    post.Replies = new ReadOnlyCollection<Post>(level2.Select(p2 =>
                        new Post(board.Site, board.Page, Convert.ToInt32(p2.Key))).ToList());
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

        using (board.Site.BeginActionScope(board))
        {
            // Refresh to get the page id.
            if (!board.Page.HasId) await board.RefreshAsync(cancellationToken);
            if (!board.Exists) yield break;
            var pagesCount = 1;
            for (int page = 1; page <= pagesCount; page++)
            {
                var jroot = await board.Site.InvokeNirvanaAsync(
                    new WikiaQueryRequestMessage(new
                    {
                        format = "json",
                        controller = "ArticleComments",
                        method = "Content",
                        articleId = board.Page.Id,
                        page = page
                    }), WikiaJsonResponseParser.Default, cancellationToken);
                // Build comment structure.
                var jcomments = jroot["commentListRaw"]?.AsObject();
                if (jcomments != null && jcomments.Count > 0)
                {
                    var comments = PostsFromJsonOutline(jcomments);
                    pagesCount = (int)jroot["pagesCount"];
                    await RefreshPostsAsync(PostsAndDescendants(comments), options, cancellationToken);
                    using (ExecutionContextStash.Capture())
                        foreach (var c in comments)
                            yield return c;
                }
            }
        }
    }

    private static readonly WikiPageQueryProvider postLastRevisionQueryProvider = new WikiPageQueryProvider
    {
        Properties = { new RevisionsPropertyProvider { FetchContent = true } }, ResolveRedirects = false
    };

    private static readonly RevisionsPropertyProvider postRevisionWithContentProvider =
        new RevisionsPropertyProvider { FetchContent = true };

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
                    var postsNeedFetchingId = partition.Where(p => p.Id == 0).ToList();
                    if (postsNeedFetchingId.Count > 0)
                    {
                        site.Logger.LogDebug("Fetching page ID for {Count} comments.", postsNeedFetchingId.Count);
                    }
                    // Fetch last revisions to determine content and last editor
                    var pages = partition.Select(p => new WikiPage(site, p.Id)).ToList();
                    var lastRevisionTask = pages.RefreshAsync(postLastRevisionQueryProvider, cancellationToken);
                    // Fetch the first revisions, when needed, to determine author.
                    Dictionary<long, Revision> firstRevisionDict = null;
                    if (partition.Count == 1
                        || (options & PostQueryOptions.ExactAuthoringInformation) == PostQueryOptions.ExactAuthoringInformation)
                    {
                        // We can only fetch for 1 page at a time, with rvdir = "newer"
                        firstRevisionDict = new Dictionary<long, Revision>();
                        foreach (var post in partition)
                        {
                            var generator = new RevisionsGenerator(site, post.Id)
                            {
                                TimeAscending = true, PaginationSize = 1, PropertyProvider = postRevisionWithContentProvider,
                            };
                            var rev = await generator.EnumItemsAsync().FirstAsync(cancellationToken);
                            firstRevisionDict[post.Id] = rev;
                        }
                    }
                    await lastRevisionTask;
                    var lastRevisions = pages.ToDictionary(p => p.Id, p => p.LastRevision);
                    foreach (var post in partition)
                    {
                        var lastRev = lastRevisions[post.Id];
                        Revision firstRev = null;
                        firstRevisionDict?.TryGetValue(post.Id, out firstRev);
                        post.SetRevisions(firstRev, lastRev);
                    }
                }
            }
        }
    }

    public static async Task<Post> PostCommentAsync(WikiaSite site, object scopeInst, WikiPageStub owner, long? parentId, string content,
        CancellationToken cancellationToken)
    {
        Debug.Assert(site != null);
        Debug.Assert(owner.HasTitle);
        using (site.BeginActionScope(scopeInst, owner, parentId))
        {
            var jresult = await site.InvokeWikiaAjaxAsync(new WikiaQueryRequestMessage(new
            {
                article = owner.Id,
                rs = "ArticleCommentsAjax",
                method = "axPost",
                token = await site.GetTokenAsync("edit", cancellationToken),
                convertToFormat = "",
                parentId = parentId,
                wpArticleComment = content,
            }, true), WikiaJsonResponseParser.Default, cancellationToken);
            if (((int?)jresult["error"] ?? 0) != 0)
                throw new OperationFailedException((string)jresult["msg"]);
            var text = (string)jresult["text"];
            var doc = new HtmlDocument();
            doc.LoadHtml(text);
            var node = doc.DocumentNode.SelectSingleNode("li");
            if (node == null)
                throw new UnexpectedDataException("Cannot locate the text node in the Wikia API response.");
            return Post.FromHtmlNode(site, owner, node);
        }
    }

    public static async Task<Post> PostWallMessageAsync(WikiaSite site, object scopeInst, WikiPageStub owner,
        string messageTitle, string messageBody, IEnumerable<string> relatedPages, CancellationToken cancellationToken)
    {
        Debug.Assert(site != null);
        Debug.Assert(owner.HasTitle);
        using (site.BeginActionScope(scopeInst, owner))
        {
            var tokenPurged = false;
            var pageTitle = owner.Title;
            var pageNamespaceId = owner.NamespaceId;
            if (pageTitle.StartsWith("Message Wall:", StringComparison.OrdinalIgnoreCase))
            {
                pageTitle = pageTitle[13..];
                if (!owner.HasNamespaceId) pageNamespaceId = WikiaNamespaces.MessageWall;
            }
            else if (pageTitle.StartsWith("Board:", StringComparison.OrdinalIgnoreCase))
            {
                pageTitle = pageTitle[6..];
                if (!owner.HasNamespaceId) pageNamespaceId = WikiaNamespaces.Thread;
            }
            else
            {
                var link = WikiLink.Parse(site, owner.Title);
                pageTitle = link.Title;
                pageNamespaceId = link.Namespace.Id;
            }
            var queryParams = new OrderedKeyValuePairs<string, object>
            {
                { "token", null },
                { "controller", "WallExternal" },
                { "method", "postNewMessage" },
                { "format", "json" },
                { "pagenamespace", pageNamespaceId },
                { "pagetitle", pageTitle },
                { "messagetitle", messageTitle },
                { "body", messageBody },
                { "notifyeveryone", 0 },
                { "convertToFormat", "" },
            };
            if (relatedPages != null)
            {
                foreach (var title in relatedPages)
                    queryParams.Add("relatedTopics[]", title);
            }
            BEGIN:
            queryParams["token"] = await site.GetTokenAsync("edit", cancellationToken);
            var jresult = await site.InvokeNirvanaAsync(new WikiaQueryRequestMessage(queryParams, true), WikiaJsonResponseParser.Default,
                cancellationToken);
            if (!string.Equals((string)jresult["status"], "True", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = (string)jresult["errormsg"];
                if (errorMessage != null)
                {
                    if (!tokenPurged)
                    {
                        if (errorMessage.Contains("There seems to be a problem with your login session",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            await site.GetTokenAsync("edit", true, cancellationToken);
                            tokenPurged = true;
                            goto BEGIN;
                        }
                    }
                }
                errorMessage = "Status code indicates a failure: " + (string)jresult["status"];
                throw new OperationFailedException(errorMessage);
            }
            var text = (string)jresult["message"];
            var doc = new HtmlDocument();
            doc.LoadHtml(text);
            var node = doc.DocumentNode.SelectSingleNode("li");
            if (node == null)
                throw new UnexpectedDataException("Cannot locate the comment text node in the Wikia API response.");
            return Post.FromHtmlNode(site, owner, node);
        }
    }

    public static async Task<Post> ReplyWallMessageAsync(WikiaSite site, object scopeInst, WikiPageStub owner,
        long parentId, string messageBody, CancellationToken cancellationToken)
    {
        Debug.Assert(site != null);
        using (site.BeginActionScope(scopeInst, owner, parentId))
        {
            var tokenPurged = false;
            BEGIN:
            var jresult = await site.InvokeNirvanaAsync(new WikiaQueryRequestMessage(new
            {
                controller = "WallExternal",
                method = "replyToMessage",
                format = "json",
                token = await site.GetTokenAsync("edit", cancellationToken),
                pagenamespace = WikiaNamespaces.Thread,
                pagetitle = parentId,
                parent = parentId,
                body = messageBody,
                convertToFormat = "",
            }, true), WikiaJsonResponseParser.Default, cancellationToken);
            if (!string.Equals((string)jresult["status"], "True", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = (string)jresult["errormsg"];
                if (errorMessage != null)
                {
                    if (!tokenPurged)
                    {
                        if (errorMessage.Contains("There seems to be a problem with your login session",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            await site.GetTokenAsync("edit", true, cancellationToken);
                            tokenPurged = true;
                            goto BEGIN;
                        }
                    }
                }
                errorMessage = "Status code indicates a failure: " + (string)jresult["status"];
                throw new OperationFailedException(errorMessage);
            }
            var text = (string)jresult["message"];
            var doc = new HtmlDocument();
            doc.LoadHtml(text);
            var node = doc.DocumentNode.SelectSingleNode("li");
            if (node == null)
                throw new UnexpectedDataException("Cannot locate the comment text node in the Wikia API response.");
            return Post.FromHtmlNode(site, owner, node);
        }
    }

    public static async Task RefreshBaordsAsync(IEnumerable<Board> boards, CancellationToken cancellationToken)
    {
        Debug.Assert(boards != null);
        // Fetch comment content.
        // You can even fetch posts from different sites.
        foreach (var sitePosts in boards.GroupBy(p => p.Site))
        {
            var site = sitePosts.Key;
            var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                ? 500
                : 50;
            foreach (var partition in sitePosts.Partition(titleLimit))
            {
                Debug.Assert(partition.All(p => p.Page.HasId || p.Page.HasTitle));
                var boardsWithTitle = partition.Where(b => b.Page.HasTitle).ToList();
                if (boardsWithTitle.Count > 0)
                {
                    var stubs = await WikiPageStub.FromPageTitles(site, boardsWithTitle.Select(b => b.Page.Title))
                        .ToListAsync(cancellationToken);
                    for (int i = 0; i < boardsWithTitle.Count; i++)
                    {
                        boardsWithTitle[i].LoadFromPageStub(stubs[i]);
                    }
                }
                var boardsWithId = partition.Where(b => !b.Page.HasTitle).ToList();
                if (boardsWithId.Count > 0)
                {
                    var stubs = await WikiPageStub.FromPageTitles(site, boardsWithId.Select(b => b.Page.Title))
                        .ToListAsync(cancellationToken);
                    for (int i = 0; i < boardsWithId.Count; i++)
                    {
                        boardsWithId[i].LoadFromPageStub(stubs[i]);
                    }
                }
            }
        }
    }

}
