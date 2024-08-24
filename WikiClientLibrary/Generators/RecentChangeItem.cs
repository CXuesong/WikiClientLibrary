using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Represents MediaWiki recent change entry.
/// </summary>
[JsonContract]
public sealed record RecentChangeItem
{

    internal WikiSite? Site { get; }

    internal RecentChangeItem() { }

    [JsonPropertyName("type")]
    [JsonInclude]
    private string TypeName
    {
        set
        {
            switch (value)
            {
                case "edit":
                    Type = RecentChangesType.Edit;
                    break;
                case "external":
                    Type = RecentChangesType.External;
                    break;
                case "new":
                    Type = RecentChangesType.Create;
                    break;
                case "move":
                    Type = RecentChangesType.Move;
                    break;
                case "log":
                    Type = RecentChangesType.Log;
                    break;
                case "categorize":
                    Type = RecentChangesType.Categorize;
                    break;
                case "move over redirect":
#pragma warning disable 612
                    Type = RecentChangesType.MoveOverRedirect;
#pragma warning restore 612
                    break;
                default:
                    throw new UnexpectedDataException(string.Format(Prompts.ExceptionUnexpectedParamValue2, nameof(TypeName), value));
            }
        }
    }

    /// <summary>Recent change type.</summary>
    [JsonIgnore]
    public RecentChangesType Type { get; private set; }

    /// <summary>Namespace ID of the page affected by this item.</summary>
    [JsonPropertyName("ns")]
    public int NamespaceId { get; init; }

    /// <summary>Full title of the page affected by this item.</summary>
    public string Title { get; init; }

    /// <summary>ID of the page affected by this item.</summary>
    public long PageId { get; init; }

    /// <summary>ID of the new revision affected by this item.</summary>
    [JsonPropertyName("revid")]
    public long RevisionId { get; init; }

    /// <summary>ID of the old revision affected by this item.</summary>
    [JsonPropertyName("old_revid")]
    public long OldRevisionId { get; init; }

    /// <summary>ID of recent change entry.</summary>
    [JsonPropertyName("rcid")]
    public long Id { get; init; }

    /// <summary>Name of the user making this recent change.</summary>
    [JsonPropertyName("user")]
    public string UserName { get; init; }

    /// <summary>The user ID who was responsible for the recent change.</summary>
    /// <remarks>When using this property with log events, there are some caveats.
    /// See <see cref="LogEventItem.UserId"/> for more information.</remarks>
    public long UserId { get; init; }

    /// <summary>Content length of the old revision affected by this item.</summary>
    [JsonPropertyName("oldlen")]
    public int OldContentLength { get; init; }

    /// <summary>Content length of the new revision affected by this item.</summary>
    [JsonPropertyName("newlen")]
    public int NewContentLength { get; init; }

    /// <summary>The difference of <see cref="NewContentLength"/> from <see cref="OldContentLength"/>.</summary>
    public int DeltaContentLength => NewContentLength - OldContentLength;

    /// <summary>The time and date of the change.</summary>
    public DateTime TimeStamp { get; init; }

    /// <summary>The edit/log comment.</summary>
    public string Comment { get; init; }

    /// <summary>The parsed comment for the edit/log comment.</summary>
    public string ParsedComment { get; init; }

    public IList<string> Tags { get; init; }

    /// <summary>SHA-1 hash of the updated revision.</summary>
    public string Sha1 { get; init; }

    public bool Redirect { get; init; }

    /// <summary>For log items, ID of the log entry.</summary>
    public int? LogId { get; init; }

    /// <summary>For log items, gets log type name.</summary>
    /// <remarks>See <see cref="LogTypes"/> for a list of predefined values.</remarks>
    public string LogType { get; init; }

    /// <summary>
    /// Specific log action.
    /// </summary>
    /// <remarks>
    /// See <see cref="LogActions"/> for a list of predefined values.
    /// To determine a specific log action, you need to first check the <see cref="LogType"/>
    /// property, because certain the same log action value may have different meaning in
    /// different log type context.
    /// </remarks>
    public string LogAction { get; init; }

    /// <summary>For log items, gets additional log parameters.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public LogParameterCollection LogParams { get; init; } = LogParameterCollection.Empty;

    [JsonIgnore]
    public RevisionFlags Flags { get; private set; }

    [JsonIgnore]
    public PatrolStatus PatrolStatus { get; private set; }

    [JsonInclude] private bool Minor;
    [JsonInclude] private bool Bot;
    [JsonInclude] private bool New;
    [JsonInclude] private bool Anon;
    [JsonInclude] private bool Patrolled;
    [JsonInclude] private bool Unpatrolled;

    // User context data is not supported as of now -- calling this function manually.
    // https://github.com/dotnet/runtime/issues/59892
    internal void OnDeserialized(WikiSite site)
    {
        Flags = RevisionFlags.None;
        if (Minor) Flags |= RevisionFlags.Minor;
        if (Bot) Flags |= RevisionFlags.Bot;
        if (New) Flags |= RevisionFlags.Create;
        if (Anon) Flags |= RevisionFlags.Anonymous;
        if (Patrolled) PatrolStatus = PatrolStatus.Patrolled;
        else if (Unpatrolled) PatrolStatus = PatrolStatus.Unpatrolled;
        else PatrolStatus = PatrolStatus.Unknown;
        if (Patrolled && Unpatrolled)
            site.Logger.LogWarning("Patrolled and Unpatrolled are both set for rcid={Id}, page {Page}.", Id, Title);
    }

    /// <summary>
    /// Asynchronously patrol the change.
    /// </summary>
    /// <exception cref="UnauthorizedOperationException">
    /// <para>You don't have permission to patrol changes. Only users with the patrol right can do this.</para>
    /// <para>OR You don't have permission to patrol your own changes. Only users with the autopatrol right can do this.</para>
    /// </exception>
    /// <remarks>It's suggested that the caller only patrol the pages whose <see cref="PatrolStatus"/> is <see cref="Generators.PatrolStatus.Unpatrolled"/>.</remarks>
    /// <exception cref="NotSupportedException">Patrolling is disabled on this wiki.</exception>
    public Task PatrolAsync()
    {
        return PatrolAsync(CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously patrol the change.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <exception cref="UnauthorizedOperationException">
    /// <para>You don't have permission to patrol changes. Only users with the patrol right can do this.</para>
    /// <para>OR You don't have permission to patrol your own changes. Only users with the <c>autopatrol</c> right can do this.</para>
    /// </exception>
    /// <remarks>It's suggested that the caller only patrol the pages whose <see cref="PatrolStatus"/> is <see cref="Generators.PatrolStatus.Unpatrolled"/>.</remarks>
    /// <exception cref="NotSupportedException">Patrolling is disabled on this wiki.</exception>
    public Task PatrolAsync(CancellationToken cancellationToken)
    {
        if (PatrolStatus == PatrolStatus.Patrolled)
            throw new InvalidOperationException(Prompts.ExceptionChangePatrolled);
        Site.AccountInfo.AssertRight(UserRights.Patrol);
        return RequestHelper.PatrolAsync(Site, Id, null, cancellationToken);
    }

    /// <summary>
    /// If the current recent change item is a log event, returns the corresponding log event item.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Type"/> is not <see cref="RecentChangesType.Log"/>.</exception>
    /// <returns>The corresponding log event item.</returns>
    public LogEventItem ToLogEventItem()
    {
        if (Type != RecentChangesType.Log)
            throw new InvalidOperationException(string.Format(Prompts.ExceptionRequireParamValue2,
                nameof(RecentChangeItem) + "." + nameof(Type), RecentChangesType.Log));
        return LogEventItem.FromRecentChangeItem(this);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Id);
        sb.Append(',');
        sb.Append(TimeStamp);
        sb.Append(',');
        if (LogType != null)
        {
            sb.Append(LogType);
            sb.Append('/');
            sb.Append(LogAction);
            sb.Append(",{");
            if (LogParams.Count > 0)
            {
                foreach (var p in LogParams)
                {
                    sb.Append(p.Key);
                    sb.Append('=');
                    sb.Append(p.Value);
                    sb.Append(',');
                }
                sb.Length--;
            }
            sb.Append("},");
        }
        sb.Append(Type);
        sb.Append(",[");
        sb.Append(Flags);
        sb.Append("],");
        sb.Append(Title);
        sb.Append(',');
        sb.Append(UserName);
        sb.Append(',');
        sb.Append(Comment);
        return sb.ToString();
    }

}

/// <summary>
/// Types of recent changes. Used in <see cref="RecentChangeItem"/>.
/// </summary>
public enum RecentChangesType
{

    Edit = 0,
    Create,
    Move,
    Log,

    /// <summary>
    /// Category membership change. (MediaWiki 1.27)
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/Manual:CategoryMembershipChanges .</remarks>
    Categorize,
    External,

    /// <summary>
    /// Move over redirect. (Obsolete.)
    /// </summary>
    [Obsolete] MoveOverRedirect,

}

/// <summary>
/// Values indicating whether the specific change has been patrolled.
/// </summary>
public enum PatrolStatus
{

    /// <summary>
    /// Not available or not applicable.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Not patrolled.
    /// </summary>
    Unpatrolled,

    /// <summary>
    /// Patrolled.
    /// </summary>
    Patrolled,

}
