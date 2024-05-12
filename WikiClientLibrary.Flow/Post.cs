using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Flow;

/// <summary>
/// Represents a normal flow post.
/// </summary>
public sealed class Post
{

    /// <summary>
    /// Initializes a new <see cref="Post"/> instance from MW site and post workflow ID.
    /// </summary>
    /// <param name="site">MediaWiki site.</param>
    /// <param name="topicTitle">The full page title of the Flow topic.</param>
    /// <param name="workflowId">Full page title of the Flow discussion board, including <c>Topic:</c> namespace prefix.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/>, <paramref name="topicTitle"/>, or <paramref name="workflowId"/> is <c>null</c>.</exception>
    public Post(WikiSite site, string topicTitle, string workflowId)
    {
        Site = site ?? throw new ArgumentNullException(nameof(site));
        TopicTitle = topicTitle ?? throw new ArgumentNullException(nameof(topicTitle));
        WorkflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
    }

    /// <summary>
    /// The MediaWiki site hosting this post.
    /// </summary>
    public WikiSite Site { get; }

    /// <summary>
    /// The full page title of the Flow topic.
    /// </summary>
    public string TopicTitle { get; private set; }

    /// <summary>
    /// Workflow ID of the post.
    /// </summary>
    public string WorkflowId { get; private set; }

    /// <summary>
    /// Gets the last revision of the post.
    /// </summary>
    public Revision LastRevision { get; private set; }

    /// <summary>
    /// Gets a read-only view of the replies.
    /// </summary>
    public IList<Post> Replies { get; private set; } = (IList<Post>)Array.Empty<Post>();

    /// <summary>
    /// Post content.
    /// </summary>
    /// <remarks>This property is set to <see cref="LastRevision"/>.<see cref="Revision.Content"/> after the refresh.</remarks>
    public string Content { get; set; }

    internal static Post FromJson(WikiSite site, JObject topicList, string workflowId)
    {
        var post = new Post(site, "Thread:dummy", workflowId);
        post.LoadFromJsonTopicList(topicList, workflowId);
        return post;
    }

    /// <inheritdoc cref="RefreshAsync(CancellationToken)"/>
    public Task RefreshAsync()
    {
        return RefreshAsync(CancellationToken.None);
    }

    /// <summary>
    /// Refreshes the topic revision and replies from the server.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <remarks>
    /// Due to inherent limitation of MW API, this method will not fetch replies nor their workflow IDs.
    /// <see cref="Revision.ReplyIds"/> in <see cref="LastRevision"/>, as well as <see cref="Replies"/>, will be empty,
    /// </remarks>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
        {
            action = "flow",
            submodule = "view-post",
            page = TopicTitle,
            vppostId = WorkflowId,
            vpformat = "wikitext",
        }), cancellationToken);
        var jtopiclist = (JObject)jresult["flow"]["view-post"]["result"]["topic"];
        var workflowId = (string)jtopiclist["roots"].First;
        LoadFromJsonTopicList(jtopiclist, workflowId);
    }

    /// <inheritdoc cref="ModerateAsync(ModerationAction,string,CancellationToken)"/>
    public Task ModerateAsync(ModerationAction action, string reason)
    {
        return ModerateAsync(action, reason, CancellationToken.None);
    }

    /// <summary>
    /// Moderates the post with the specified action.
    /// </summary>
    /// <param name="action">The action to perform. You need to have sufficient permission for it.</param>
    /// <param name="reason">The reason for moderation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <remarks>This method will not update <see cref="LastRevision"/> content, you need to call <see cref="RefreshAsync()"/> if you need the latest revision information.</remarks>
    public async Task ModerateAsync(ModerationAction action, string reason, CancellationToken cancellationToken)
    {
        if (reason == null) throw new ArgumentNullException(nameof(reason));
        if (reason.Length == 0) throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        JToken jresult;
        using (await Site.ModificationThrottler.QueueWorkAsync("Moderate: " + WorkflowId, cancellationToken))
        {
            jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "flow",
                submodule = "moderate-post",
                token = WikiSiteToken.Edit,
                page = TopicTitle,
                mppostId = WorkflowId,
                mpmoderationState = EnumParser.ToString(action),
                mpreason = reason,
            }), cancellationToken);
        }
    }

    // topicList: The topiclist node of a view-topiclist query result.
    internal void LoadFromJsonTopicList(JObject topicList, string workflowId)
    {
        var revisionId = (string)topicList["posts"][workflowId]?.First;
        if (revisionId == null)
            throw new ArgumentException("Cannot find workflow ID " + workflowId + " in [posts] array.", nameof(workflowId));
        var jrevision = (JObject)topicList["revisions"][revisionId];
        if (jrevision == null)
            throw new UnexpectedDataException("Cannot find revision " + revisionId + " in [revisions] array.");
        var rev = jrevision.ToObject<Revision>(FlowUtility.FlowJsonSerializer);
        LastRevision = rev;
        if (rev.ReplyIds == null || rev.ReplyIds.Count == 0)
        {
            Replies = Array.Empty<Post>();
        }
        else
        {
            var posts = new List<Post>(rev.ReplyIds.Count);
            posts.AddRange(rev.ReplyIds.Select(pid => FromJson(Site, topicList, pid)));
            Replies = new ReadOnlyCollection<Post>(posts);
        }
        WorkflowId = workflowId;
        TopicTitle = rev.ArticleTitle;
        Content = LastRevision.Content;
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
    public Task<Post> ReplyAsync(string content, CancellationToken cancellationToken)
    {
        return FlowRequestHelper.ReplyAsync(Site, TopicTitle, WorkflowId, content, cancellationToken);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var result = "[" + WorkflowId + "]";
        if (Replies.Count > 0) result += "[Re:" + Replies.Count + "]";
        return result;
    }

}
