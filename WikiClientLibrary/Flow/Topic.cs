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
        private readonly List<Revision> _Posts = new List<Revision>();

        /// <summary>
        /// Initializes a new <see cref="Board"/> instance from MW site and board page title.
        /// </summary>
        /// <param name="site">MediaWiki site.</param>
        /// <param name="title">Full page title of the Flow discussion board, including namespace prefix.</param>
        public Topic(WikiSite site, string title)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            LoggerFactory = site.LoggerFactory;
            Posts = new ReadOnlyCollection<Revision>(_Posts);
        }

        internal Topic(WikiSite site)
        {
            Debug.Assert(site != null);
            Site = site;
            LoggerFactory = site.LoggerFactory;
            Posts = new ReadOnlyCollection<Revision>(_Posts);
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
            _Posts.Clear();
            TopicTitleRevision = null;
            WorkflowId = null;
            if (_Posts.Capacity < topicList.Count) _Posts.Capacity = topicList.Count;
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
            // TODO Parse the replies.
            foreach (var replyIds in rev.Replies)
            {
                
            }
        }

        public IList<Revision> Posts { get; }

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
