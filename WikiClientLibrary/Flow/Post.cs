using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Sites;

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
        /// <param name="workflowId">Full page title of the Flow discussion board, including <c>Topic:</c> namespace prefix.</param>
        public Post(WikiSite site, string workflowId)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            WorkflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
        }

        /// <summary>
        /// The MediaWiki site hosting this post.
        /// </summary>
        public WikiSite Site { get; }

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
            var post = new Post(site, workflowId);
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
