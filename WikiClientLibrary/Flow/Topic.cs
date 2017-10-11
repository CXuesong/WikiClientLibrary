using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Reporesents a topic on a Flow board.
    /// </summary>
    public class Topic : IWikiClientLoggable
    {
        private ILoggerFactory _LoggerFactory;
        private ILogger logger = NullLogger.Instance;

        /// <summary>
        /// Initializes a new <see cref="Topic"/> instance from MW site and topic page title.
        /// </summary>
        /// <param name="site">MediaWiki site.</param>
        /// <param name="title">Full page title of the Flow discussion board, including <c>Topic:</c> namespace prefix.</param>
        public Topic(WikiSite site, string title)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            LoggerFactory = site.LoggerFactory;
        }

        private Topic(WikiSite site)
        {
            Debug.Assert(site != null);
            Site = site;
            LoggerFactory = site.LoggerFactory;
        }

        /// <summary>
        /// The MediaWiki site hosting this topic.
        /// </summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Full title of the topic page.
        /// </summary>
        /// <remarks>The page title is usually <see cref="WorkflowId"/> with <c>Topic:</c> namespace prefix.</remarks>
        /// <seealso cref="TopicTitleRevision"/>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the latest revision of the topic display title.
        /// </summary>
        public Revision TopicTitleRevision { get; private set; }

        internal static IEnumerable<Topic> FromJsonTopicList(WikiSite site, JObject topicList)
        {
            return topicList["roots"].Select(rootId =>
            {
                var topic = new Topic(site);
                topic.LoadFromJsonTopicList(topicList, (string)rootId);
                return topic;
            });
        }

        // topicList: The topiclist node of a view-topiclist query result.
        internal void LoadFromJsonTopicList(JObject topicList, string workflowId)
        {
            TopicTitleRevision = null;
            WorkflowId = null;
            var revisionId = (string)topicList["posts"][workflowId]?.First;
            if (revisionId == null)
                throw new ArgumentException("Cannot find workflow ID " + workflowId + " in [posts] array.", nameof(workflowId));
            var jrevision = (JObject)topicList["revisions"][revisionId];
            if (jrevision == null)
                throw new UnexpectedDataException("Cannot find revision " + revisionId + " in [revisions] array.");
            var rev = jrevision.ToObject<Revision>(FlowUtility.FlowJsonSerializer);
            // Assume the first post as title.
            TopicTitleRevision = rev;
            Title = TopicTitleRevision.ArticleTitle;
            WorkflowId = TopicTitleRevision.WorkflowId;
            if (rev.ReplyIds == null || rev.ReplyIds.Count == 0)
            {
                Posts = Post.EmptyPosts;
            }
            else
            {
                var posts = new List<Post>(rev.ReplyIds.Count);
                posts.AddRange(rev.ReplyIds.Select(pid => Post.FromJson(Site, topicList, pid)));
                Posts = new ReadOnlyCollection<Post>(posts);
            }
            WorkflowId = workflowId;
        }

        public IList<Post> Posts { get; private set; }

        /// <summary>
        /// Workflow ID of the topic.
        /// </summary>
        /// <remarks>Workflow ID is usually <see cref="Title"/> stripped of <c>Topic:</c> namespace prefix.</remarks>
        public string WorkflowId { get; private set; }

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
        }

        /// <summary>
        /// Returns the user-friendly title of the topic.
        /// </summary>
        public override string ToString()
        {
            var result = "[" + Title + "]";
            if (TopicTitleRevision != null) result += TopicTitleRevision.Content;
            return result;
        }
    }
}
