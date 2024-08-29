using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Flow;

/// <summary>
/// Contains the revision information with content of Flow board headers or topic posts.
/// </summary>
[JsonContract]
public sealed class Revision
{

    private static readonly IDictionary<string, FlowLink> emptyLinks = new ReadOnlyDictionary<string, FlowLink>(
        new Dictionary<string, FlowLink>());

    /// <summary>
    /// Workflow ID of the revision. For board header, this is the workflow ID of the board;
    /// for topic posts, this is the topic ID.
    /// </summary>
    public string WorkflowId { get; init; }

    /// <summary>
    /// Full title of the page containing this revision.
    /// </summary>
    public string ArticleTitle { get; init; }

    /// <summary>
    /// Flow revision ID.
    /// </summary>
    public string RevisionId { get; init; }

    /// <summary>
    /// The time stamp of the revision.
    /// </summary>
    public DateTime TimeStamp { get; init; }

    [JsonInclude]
    [JsonPropertyName("timestamp")]
    private string RawTimeStamp
    {
        init => TimeStamp = DateTime.ParseExact(value, "yyyyMMddHHmmss", null);
    }

    /// <summary>
    /// For topic revision, the last update time of the whole topic,
    /// including replies, changes to topic title, content, or replies.
    /// </summary>
    [JsonIgnore]
    public DateTime? LastUpdated { get; init; }

    [JsonInclude]
    [JsonPropertyName("last_updated")]
    private long RawLastUpdated
    {
        init => LastUpdated = FlowUtility.DateFromJavaScriptTicks(value);
    }

    [JsonIgnore]
    public FlowRevisionAction ChangeType { get; init; }

    [JsonInclude]
    [JsonPropertyName("changeType")]
    private string RawChangeType
    {
        init => ChangeType = ParseRevisionAction(value);
    }

    // "dateFormats": { "timeAndDate": "05:44, 11 October 2017", "date": "11 October 2017", "time": "05:44"}
    //[JsonProperty]
    //public DateFormats DateFormats { get; init; }

    // Can be JObject, or empty JArray
    public JsonNode Properties { get; init; }

    public bool IsOriginalContent { get; init; }

    /// <summary>
    /// Determines whether the post has been moderated (hidden).
    /// </summary>
    public bool IsModerated { get; init; }

    /// <summary>
    /// Determines whether the post has been locked (i.e. discussion closed).
    /// </summary>
    public bool IsLocked { get; init; }

    /// <summary>
    /// Determines whether the post has been moderated but not locked.
    /// </summary>
    public bool IsModeratedNotLocked { get; init; }

    [JsonIgnore]
    public ModerationState ModerationState { get; init; }

    [JsonInclude]
    [JsonPropertyName("moderateState")]
    public string RawModerationState
    {
        init => ModerationState = EnumParser.ParseModerationState(value);
    }

    [JsonIgnore]
    public string ModerationReason { get; init; }

    [JsonInclude]
    [JsonPropertyName("moderateReason")]
    public JsonNode RawModerationReason
    {
        init => ModerationReason = (string)value["content"];
    }

    public bool IsMaxThreadingDepth { get; init; }

    /// <summary>
    /// Workflow ID of the replies.
    /// </summary>
    [JsonPropertyName("replies")]
    public IList<string> ReplyIds { get; init; } = (IList<string>)Array.Empty<string>();

    /// <summary>
    /// HTML links to show different views.
    /// </summary>
    public IDictionary<string, FlowLink> Links { get; init; } = emptyLinks;

    [JsonInclude]
    [JsonPropertyName("links")]
    private JsonNode RawLinks
    {
        init
        {
            if (value is JsonObject obj)
                Links = new ReadOnlyDictionary<string, FlowLink>(
                    obj.Deserialize<Dictionary<string, FlowLink>>(FlowUtility.FlowJsonSerializer));
            else if (value is JsonArray array && array.Count == 0)
                Links = emptyLinks;
            else
                throw new ArgumentException("Cannot parse JSON value.", nameof(value));
        }
    }

    /// <summary>
    /// HTML links to show operations.
    /// </summary>
    [JsonIgnore]
    public IDictionary<string, FlowLink> Actions { get; init; } = emptyLinks;

    [JsonInclude]
    [JsonPropertyName("actions")]
    private JsonNode RawActions
    {
        init
        {
            if (value is JsonObject obj)
                Actions = new ReadOnlyDictionary<string, FlowLink>(
                    obj.Deserialize<Dictionary<string, FlowLink>>(FlowUtility.FlowJsonSerializer));
            else if (value is JsonArray array && array.Count == 0)
                Actions = emptyLinks;
            else
                throw new ArgumentException("Cannot parse JSON value.", nameof(value));
        }
    }

    /// <summary>
    /// Content length before this revision, in bytes.
    /// </summary>
    [JsonIgnore]
    public int OldContentLength { get; init; }

    /// <summary>
    /// Content length, in bytes.
    /// </summary>
    [JsonIgnore]
    public int ContentLength { get; init; }

    [JsonInclude]
    private JsonNode Size
    {
        init
        {
            OldContentLength = (int)value["old"];
            ContentLength = (int)value["new"];
        }
    }

    /// <summary>
    /// Author of the post or header.
    /// </summary>
    public UserStub Author { get; init; }

    /// <summary>
    /// Last editor of the post or header.
    /// </summary>
    public UserStub LastEditUser { get; init; }

    public UserStub Moderator { get; init; }

    /// <summary>
    /// Revision ID of the last edit.
    /// </summary>
    public string LastEditId { get; init; }

    /// <summary>
    /// Revision ID of the previous revision.
    /// </summary>
    public string PreviousRevisionId { get; init; }

    /// <summary>
    /// Workflow ID of the post this revision replies to.
    /// </summary>
    public string ReplyToId { get; init; }

    /// <summary>
    /// Revision content, in wikitext format.
    /// Depending on the context, this can be the topic content, topic summary, or post content.
    /// </summary>
    [JsonIgnore]
    public string Content { get; init; }

    [JsonInclude]
    [JsonPropertyName("content")]
    private JsonNode RawContent
    {
        init
        {
            if (value == null)
                Content = null;
            else
                Content = (string)value["content"];
        }
    }

    /// <summary>
    /// For topic title revision, this is the latest revision of topic summary, if available.
    /// </summary>
    [JsonIgnore]
    public Revision Summary { get; init; }

    [JsonInclude]
    [JsonPropertyName("summary")]
    private JsonNode RawSummary
    {
        init => Summary = value["revision"]?.Deserialize<Revision>();
    }

    private static readonly Dictionary<string, FlowRevisionAction> flowActionsDict = new Dictionary<string, FlowRevisionAction>
    {
        { "create-header", FlowRevisionAction.CreateHeader },
        { "edit-header", FlowRevisionAction.EditHeader },
        { "hide-post", FlowRevisionAction.HidePost },
        { "hide-topic", FlowRevisionAction.HideTopic },
        { "delete-post", FlowRevisionAction.DeletePost },
        { "delete-topic", FlowRevisionAction.DeleteTopic },
        { "suppress-post", FlowRevisionAction.SuppressPost },
        { "suppress-topic", FlowRevisionAction.SuppressTopic },
        { "restore-post", FlowRevisionAction.RestorePost },
        { "restore-topic", FlowRevisionAction.RestoreTopic },
        { "lock-topic", FlowRevisionAction.LockTopic },
        { "new-topic", FlowRevisionAction.NewTopic },
        { "reply", FlowRevisionAction.Reply },
        { "edit-post", FlowRevisionAction.EditPost },
        { "edit-title", FlowRevisionAction.EditTitle },
        { "create-topic-summary", FlowRevisionAction.CreateTopicSummary },
        { "edit-topic-summary", FlowRevisionAction.EditTopicSummary },
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
[JsonContract]
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

    public string Url { get; }

    public string Title { get; }

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
