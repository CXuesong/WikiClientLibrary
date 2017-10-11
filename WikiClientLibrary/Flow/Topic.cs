using System;
using System.Collections.Generic;
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
        /// Initializes a new <see cref="Board"/> instance from MW site and board page title.
        /// </summary>
        /// <param name="site">MediaWiki site.</param>
        /// <param name="title">Full page title of the Flow discussion board, including namespace prefix.</param>
        public Topic(WikiSite site, string title)
        {
            Site = site;
            Title = title;
            LoggerFactory = site.LoggerFactory;
        }

        private Topic(WikiSite site)
        {
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
        /// <seealso cref="TopicTitle"/>
        public string Title { get; private set; }

        /// <param name="topicList">The topiclist node of a view-topiclist query result.</param>
        internal static IList<Topic> FromJsonTopicListResult(WikiSite site, JObject topicList)
        {
            Debug.Assert(site != null);
            return topicList["roots"].Select(jroot =>
            {
                var rootId = (string)jroot;
                var revisionId = (string)topicList["posts"][rootId].First;
                var revision = (JObject)topicList["revisions"][revisionId];
                var topic = new Topic(site);
                topic.FillFromRevision(revision);
                return topic;
            }).ToList();
        }

        protected void FillFromRevision(JObject jrevision)
        {
            WorkflowId = (string)jrevision["workflowId"];
            // Yes, they are not lower-case. They are camel-case.
            Title = (string)jrevision["articleTitle"];
            TopicTitle = (string)jrevision["content"]["content"];
            IsLocked = (bool)jrevision["isLocked"];
            IsModerated = (bool)jrevision["isModerated"];
            var expr = (string)jrevision["timestamp"];
            TimeStamp = DateTime.ParseExact(expr, "yyyyMMddHHmmss", null, DateTimeStyles.AssumeUniversal);
        }

        /// <summary>
        /// Title of the topic, in wikitext format.
        /// </summary>
        public string TopicTitle { get; set; }

        /// <summary>
        /// Whether the topic has been locked a.k.a. "marked as resolved".
        /// </summary>
        public bool IsLocked { get; private set; }

        public bool IsModerated { get; private set; }

        public DateTime TimeStamp { get; private set; }

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

    }
}
