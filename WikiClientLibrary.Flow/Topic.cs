using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
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
        /// <param name="title">Either full page title of the Flow discussion board including <c>Topic:</c> namespace prefix, or the workflow ID of the board.</param>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> or <paramref name="title"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="title"/> is not a valid title.</exception>
        public Topic(WikiSite site, string title)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            var link = WikiLink.Parse(site, title, FlowNamespaces.Topic);
            Title = link.ToString();
            WorkflowId = link.Title.ToLowerInvariant();
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
        /// <remarks>Usually this revision shares the same <see cref="Revision.WorkflowId"/> as the topic itself.</remarks>
        public Revision TopicTitleRevision { get; private set; }

        /// <summary>
        /// Gets the list of posts, starting from the OP's first post.
        /// </summary>
        public IList<Post> Posts { get; private set; } = Post.EmptyPosts;
        
        /// <summary>
        /// Topic display title.
        /// </summary>
        /// <remarks>This property is set to <see cref="TopicTitleRevision"/>.<see cref="Revision.Content"/> after the refresh.</remarks>
        public string TopicTitle { get; set; }

        /// <summary>
        /// Topic summary.
        /// </summary>
        /// <remarks>This property is set to <see cref="TopicTitleRevision"/>.<see cref="Revision.Summary"/>.<see cref="Revision.Content"/> after the refresh.</remarks>
        public string Summary { get; set; }

        /// <summary>
        /// Workflow ID of the topic.
        /// </summary>
        /// <remarks>Workflow ID is usually <see cref="Title"/> stripped of <c>Topic:</c> namespace prefix.</remarks>
        public string WorkflowId { get; private set; }
        
        /// <inheritdoc cref="RefreshAsync(CancellationToken)"/>
        public Task RefreshAsync()
        {
            return RefreshAsync(CancellationToken.None);
        }

        /// <summary>
        /// Refreshes the topic revision and replies from the server.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "flow",
                submodule = "view-topic",
                page = Title,
                vtformat = "wikitext",
            }), cancellationToken);
            var jtopiclist = (JObject)jresult["flow"]["view-topic"]["result"]["topic"];
            var workflowId = (string)jtopiclist["roots"].First;
            LoadFromJsonTopicList(jtopiclist, workflowId);
        }

        /// <inheritdoc cref="ModerateAsync(ModerationAction,string,CancellationToken)"/>
        public Task ModerateAsync(ModerationAction action, string reason)
        {
            return ModerateAsync(action, reason, CancellationToken.None);
        }

        /// <inheritdoc cref="LockAsync(LockAction,string,CancellationToken)"/>
        /// <summary>Locks (aka. close) the topic with the specified action.</summary>
        public Task LockAsync(string reason)
        {
            return LockAsync(LockAction.Lock, reason, CancellationToken.None);
        }

        /// <inheritdoc cref="LockAsync(LockAction,string,CancellationToken)"/>
        public Task LockAsync(LockAction action, string reason)
        {
            return LockAsync(action, reason, CancellationToken.None);
        }

        /// <summary>
        /// Locks or unlocks (aka. close or reopen) the topic with the specified action.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <param name="reason">The reason for operation.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <remarks>This method will not update <see cref="TopicTitleRevision"/> content, you need to call <see cref="RefreshAsync()"/> if you need the latest revision information.</remarks>
        public async Task LockAsync(LockAction action, string reason, CancellationToken cancellationToken)
        {
            if (reason == null) throw new ArgumentNullException(nameof(reason));
            if (reason.Length == 0) throw new ArgumentException("Reason cannot be empty.", nameof(reason));
            JToken jresult;
            using (await Site.ModificationThrottler.QueueWorkAsync("Moderation", cancellationToken))
            {
                jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
                {
                    action = "flow",
                    submodule = "lock-topic",
                    token = WikiSiteToken.Edit,
                    page = Title,
                    cotmoderationState = EnumParser.ToString(action),
                    cotreason = reason,
                }), cancellationToken);
            }
        }

        /// <inheritdoc cref="UpdateSummaryAsync(CancellationToken)"/>
        public Task UpdateSummaryAsync()
        {
            return UpdateSummaryAsync(new CancellationToken());
        }

        /// <summary>
        /// Updates the topic summary to the value of <see cref="Summary"/>.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <remarks>This method will not update <see cref="TopicTitleRevision"/> content, you need to call <see cref="RefreshAsync()"/> if you need the latest revision information.</remarks>
        public async Task UpdateSummaryAsync(CancellationToken cancellationToken)
        {
            JToken jresult;
            using (await Site.ModificationThrottler.QueueWorkAsync("UpdateSummary", cancellationToken))
            {
                jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
                {
                    action = "flow",
                    submodule = "edit-topic-summary",
                    token = WikiSiteToken.Edit,
                    page = Title,
                    etsprev_revision = TopicTitleRevision?.Summary?.RevisionId,
                    etsformat = "wikitext",
                    etssummary = Summary ?? "",
                }), cancellationToken);
            }
        }

        /// <inheritdoc cref="UpdateTopicTitleAsync(CancellationToken)"/>
        public Task UpdateTopicTitleAsync()
        {
            return UpdateTopicTitleAsync(CancellationToken.None);
        }

        /// <summary>
        /// Updates the topic display title to the value of <see cref="TopicTitle"/>.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <remarks>This method will not update <see cref="TopicTitleRevision"/> content, you need to call <see cref="RefreshAsync()"/> if you need the latest revision information.</remarks>
        public async Task UpdateTopicTitleAsync(CancellationToken cancellationToken)
        {
            JToken jresult;
            using (await Site.ModificationThrottler.QueueWorkAsync("UpdateTitle", cancellationToken))
            {
                jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
                {
                    action = "flow",
                    submodule = "edit-title",
                    token = WikiSiteToken.Edit,
                    page = Title,
                    etprev_revision = TopicTitleRevision?.RevisionId,
                    etcontent = TopicTitle ?? "",
                }), cancellationToken);
            }
        }

        /// <summary>
        /// Moderates the topic with the specified action.
        /// </summary>
        /// <param name="action">The action to perform. You need to have sufficient permission for it.</param>
        /// <param name="reason">The reason for moderation.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <remarks>This method will not update <see cref="TopicTitleRevision"/> content, you need to call <see cref="RefreshAsync()"/> if you need the latest revision information.</remarks>
        public async Task ModerateAsync(ModerationAction action, string reason, CancellationToken cancellationToken)
        {
            if (reason == null) throw new ArgumentNullException(nameof(reason));
            if (reason.Length == 0) throw new ArgumentException("Reason cannot be empty.", nameof(reason));
            JToken jresult;
            using (await Site.ModificationThrottler.QueueWorkAsync("Moderation", cancellationToken))
            {
                jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
                {
                    action = "flow",
                    submodule = "moderate-topic",
                    token = WikiSiteToken.Edit,
                    page = Title,
                    mtmoderationState = EnumParser.ToString(action),
                    mtreason = reason,
                }), cancellationToken);
            }
        }

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
            if (topicList == null) throw new ArgumentNullException(nameof(topicList));
            if (workflowId == null) throw new ArgumentNullException(nameof(workflowId));
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
            Title = rev.ArticleTitle;
            WorkflowId = rev.WorkflowId;
            TopicTitle = rev.Content;
            Summary = rev.Summary?.Content;
        }

        /// <inheritdoc cref="ReplyAsync(string,CancellationToken)"/>
        public Task<Post> ReplyAsync(string content)
        {
            return ReplyAsync(content, CancellationToken.None);
        }

        /// <summary>
        /// Add a new reply to the topic.
        /// </summary>
        /// <param name="content">The content in reply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A new post containing the workflow ID of the new post.</returns>
        public Task<Post> ReplyAsync(string content, CancellationToken cancellationToken)
        {
            return FlowRequestHelper.ReplyAsync(Site, Title, WorkflowId, content, cancellationToken);
        }

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set { /*logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());*/ }
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
