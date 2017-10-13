using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Contains the revision information with content of Flow board headers or topic posts.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Revision
    {

        private static readonly IList<string> emptyStrings = new string[] { };

        private static readonly IDictionary<string, FlowLink> emptyLinks = new ReadOnlyDictionary<string, FlowLink>(
            new Dictionary<string, FlowLink>());

        /// <summary>
        /// Workflow ID of the revision. For board header, this is the workflow ID of the board;
        /// for topic posts, this is the topic ID.
        /// </summary>
        [JsonProperty]
        public string WorkflowId { get; private set; }

        /// <summary>
        /// Full title of the page containing this revision.
        /// </summary>
        [JsonProperty]
        public string ArticleTitle { get; private set; }

        /// <summary>
        /// Flow revision ID.
        /// </summary>
        [JsonProperty]
        public string RevisionId { get; private set; }

        /// <summary>
        /// The time stamp of the revision.
        /// </summary>
        public DateTime TimeStamp { get; private set; }

        [JsonProperty("timestamp")]
        private string RawTimeStamp
        {
            set => TimeStamp = DateTime.ParseExact(value, "yyyyMMddHHmmss", null);
        }

        public DateTime? LastUpdated { get; private set; }

        [JsonProperty("last_updated")]
        private long RawLastUpdated
        {
            set => LastUpdated = FlowUtility.DateFromJavaScriptTicks(value);
        }

        public FlowRevisionAction ChangeType { get; private set; }

        [JsonProperty("changeType")]
        private string RawChangeType
        {
            set => ChangeType = ParseRevisionAction(value);
        }

        // "dateFormats": { "timeAndDate": "05:44, 11 October 2017", "date": "11 October 2017", "time": "05:44"}
        //[JsonProperty]
        //public DateFormats DateFormats { get; private set; }

        // Can be JObject, or empty JArray
        [JsonProperty]
        public JToken Properties { get; private set; }

        [JsonProperty]
        public bool IsOriginalContent { get; private set; }

        /// <summary>
        /// Determines whether the post has been moderated (hidden).
        /// </summary>
        [JsonProperty]
        public bool IsModerated { get; private set; }

        /// <summary>
        /// Determines whether the post has been locked (i.e. discussion closed).
        /// </summary>
        [JsonProperty]
        public bool IsLocked { get; private set; }

        /// <summary>
        /// Determines whether the post has been moderated but not locked.
        /// </summary>
        [JsonProperty]
        public bool IsModeratedNotLocked { get; private set; }

        public ModerationState ModerationState { get; private set; }

        [JsonProperty("moderateState")]
        public string RawModerationState
        {
            set => ModerationState = EnumParser.ParseModerationState(value);
        }

        public string ModerationReason { get; private set; }

        [JsonProperty("moderateReason")]
        public JToken RawModerationReason
        {
            set => ModerationReason = (string)value["content"];
        }

        [JsonProperty]
        public bool IsMaxThreadingDepth { get; private set; }

        /// <summary>
        /// Workflow ID of the replies.
        /// </summary>
        [JsonProperty("replies")]
        public IList<string> ReplyIds { get; private set; } = emptyStrings;

        /// <summary>
        /// HTML links to show different views.
        /// </summary>
        [JsonProperty]
        public IDictionary<string, FlowLink> Links { get; private set; } = emptyLinks;

        /// <summary>
        /// HTML links to show operations.
        /// </summary>
        [JsonProperty]
        public IDictionary<string, FlowLink> Actions { get; private set; } = emptyLinks;

        /// <summary>
        /// Content length before this revision, in bytes.
        /// </summary>
        public int OldContentLength { get; private set; }

        /// <summary>
        /// Content length, in bytes.
        /// </summary>
        public int ContentLength { get; private set; }

        [JsonProperty]
        private JToken Size
        {
            set
            {
                OldContentLength = (int)value["old"];
                ContentLength = (int)value["new"];
            }
        }

        /// <summary>
        /// Author of the post or header.
        /// </summary>
        [JsonProperty]
        public UserStub Author { get; private set; }

        /// <summary>
        /// Last editor of the post or header.
        /// </summary>
        [JsonProperty]
        public UserStub LastEditUser { get; private set; }

        [JsonProperty]
        public UserStub Moderator { get; private set; }

        /// <summary>
        /// Revision ID of the last edit.
        /// </summary>
        [JsonProperty]
        public string LastEditId { get; private set; }

        /// <summary>
        /// Revision ID of the previous revision.
        /// </summary>
        [JsonProperty]
        public string PreviousRevisionId { get; private set; }

        /// <summary>
        /// Workflow ID of the post this revision replies to.
        /// </summary>
        [JsonProperty]
        public string ReplyToId { get; private set; }

        /// <summary>
        /// Revision content, in wikitext format.
        /// </summary>
        public string Content { get; private set; }

        [JsonProperty("content")]
        public JToken RawContent
        {
            set
            {
                if (value == null || value.Type == JTokenType.Null)
                    Content = null;
                else
                    Content = (string)value["content"];
            }
        }

        private static readonly Dictionary<string, FlowRevisionAction> flowActionsDict = new Dictionary<string, FlowRevisionAction>
        {
            {"create-header", FlowRevisionAction.CreateHeader},
            {"edit-header", FlowRevisionAction.EditHeader},
            {"hide-post", FlowRevisionAction.HidePost},
            {"hide-topic", FlowRevisionAction.HideTopic},
            {"delete-post", FlowRevisionAction.DeletePost},
            {"delete-topic", FlowRevisionAction.DeleteTopic},
            {"suppress-post", FlowRevisionAction.SuppressPost},
            {"suppress-topic", FlowRevisionAction.SuppressTopic},
            {"restore-post", FlowRevisionAction.RestorePost},
            {"restore-topic", FlowRevisionAction.RestoreTopic},
            {"lock-topic", FlowRevisionAction.LockTopic},
            {"new-topic", FlowRevisionAction.NewTopic},
            {"reply", FlowRevisionAction.Reply},
            {"edit-post", FlowRevisionAction.EditPost},
            {"edit-title", FlowRevisionAction.EditTitle},
            {"create-topic-summary", FlowRevisionAction.CreateTopicSummary},
            {"edit-topic-summary", FlowRevisionAction.EditTopicSummary},
        };

        private static FlowRevisionAction ParseRevisionAction(string action)
        {
            if (action == null) return FlowRevisionAction.Unknown;
            if (flowActionsDict.TryGetValue(action, out var v)) return v;
            return FlowRevisionAction.Unknown;
        }
    }

    /// <summary>
    /// Represents an HTML link to perform operations in Flow.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class FlowLink
    {
        public FlowLink(string url, string text) : this(url, text, text)
        {
        }

        [JsonConstructor]
        public FlowLink(string url, string title, string text)
        {
            Url = url;
            Title = title;
            Text = text;
        }

        [JsonProperty]
        public string Url { get; }

        [JsonProperty]
        public string Title { get; }

        [JsonProperty]
        public string Text { get; }

    }

    /// <summary>
    /// The type of action performed on each Flow revision.
    /// </summary>
    public enum FlowRevisionAction
    {
        Unknown = 0,
        CreateHeader,
        EditHeader,
        HidePost,
        HideTopic,
        DeletePost,
        DeleteTopic,
        SuppressPost,
        SuppressTopic,
        RestorePost,
        RestoreTopic,
        LockTopic,
        NewTopic,
        Reply,
        EditPost,
        EditTitle,
        CreateTopicSummary,
        EditTopicSummary,
    }
}
