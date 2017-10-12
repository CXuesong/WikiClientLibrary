using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Sites;
using System.Threading.Tasks;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Represents a normal flow post.
    /// </summary>
    public sealed class Post
    {

        internal static readonly IList<Post> EmptyPosts = new Post[] { };

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
        public IList<Post> Replies { get; private set; } = EmptyPosts;

        internal static Post FromJson(WikiSite site, JObject topicList, string workflowId)
        {
            var post = new Post(site, "Thread:dummy", workflowId);
            post.LoadFromJsonTopicList(topicList, workflowId);
            return post;
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
            if (rev.ReplyIds == null || rev.ReplyIds.Count == 0)
            {
                Replies = EmptyPosts;
            }
            else
            {
                var posts = new List<Post>(rev.ReplyIds.Count);
                posts.AddRange(rev.ReplyIds.Select(pid => FromJson(Site, topicList, pid)));
                Replies = new ReadOnlyCollection<Post>(posts);
            }
            WorkflowId = workflowId;
            TopicTitle = rev.ArticleTitle;
        }

        /// <inheritdoc cref="ReplyAsync(string,CancellationToken)"/>
        public Task<Post> ReplyAsync(string content)
        {
            return ReplyAsync(content, new CancellationToken());
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
}
