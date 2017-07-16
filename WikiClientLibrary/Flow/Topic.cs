using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Reporesents a topic on a Flow board.
    /// </summary>
    public class Topic : WikiPage
    {
        /// <inheritdoc />
        public Topic(WikiSite site, string title) : this(site, title, FlowNamespaces.Topic)
        {

        }

        /// <inheritdoc />
        public Topic(WikiSite site, string title, int defaultNamespaceId) : base(site, title, defaultNamespaceId)
        {

        }
        
        /// <inheritdoc />
        internal Topic(WikiSite site) : base(site)
        {

        }

        /// <param name="topicList">The topiclist node of a view-topiclist query result.</param>
        internal static IList<Topic> FromJsonTopicListResult(WikiSite site, JObject topicList)
        {
            Debug.Assert(site != null);
            return topicList["roots"].Select(jroot =>
            {
                var rootId = (string) jroot;
                var revisionId = (string) topicList["posts"][rootId].First;
                var revision = (JObject) topicList["revisions"][revisionId];
                var topic = new Topic(site);
                topic.FillFromRevision(revision);
                return topic;
            }).ToList();
        }

        protected void FillFromRevision(JObject jrevision)
        {
            WorkflowId = (string) jrevision["workflowId"];
            // Yes, they are not lower-case. They are camel-case.
            Title = (string) jrevision["articleTitle"];
            TopicTitle = (string) jrevision["content"]["content"];
            IsLocked = (bool) jrevision["isLocked"];
            IsModerated = (bool) jrevision["isModerated"];
            var expr = (string) jrevision["timestamp"];
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

        public string WorkflowId { get; private set; }
    }
}
