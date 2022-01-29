using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents MediaWiki recent change entry.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class RecentChangeItem
    {

        private WikiSite Site { get; }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        internal RecentChangeItem(WikiSite site)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        [JsonProperty("type")]
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
        public RecentChangesType Type { get; private set; }

        /// <summary>Namespace ID of the page affected by this item.</summary>
        [JsonProperty("ns")]
        public int NamespaceId { get; private set; }

        /// <summary>Full title of the page affected by this item.</summary>
        [JsonProperty]
        public string Title { get; private set; }

        /// <summary>ID of the page affected by this item.</summary>
        [JsonProperty]
        public int PageId { get; private set; }

        /// <summary>ID of the new revision affected by this item.</summary>
        [JsonProperty("revid")]
        public int RevisionId { get; private set; }

        /// <summary>ID of the old revision affected by this item.</summary>
        [JsonProperty("old_revid")]
        public int OldRevisionId { get; private set; }

        /// <summary>ID of recent change entry.</summary>
        [JsonProperty("rcid")]
        public int Id { get; private set; }

        /// <summary>Name of the user making this recent change.</summary>
        [JsonProperty("user")]
        public string UserName { get; private set; }

        /// <summary>The user ID who was responsible for the recent change.</summary>
        /// <remarks>When using this property with log events, there are some caveats.
        /// See <see cref="LogEventItem.UserId"/> for more information.</remarks>
        [JsonProperty]
        public int UserId { get; private set; }

        /// <summary>Content length of the old revision affected by this item.</summary>
        [JsonProperty("oldlen")]
        public int OldContentLength { get; private set; }

        /// <summary>Content length of the new revision affected by this item.</summary>
        [JsonProperty("newlen")]
        public int NewContentLength { get; private set; }

        /// <summary>The difference of <see cref="NewContentLength"/> from <see cref="OldContentLength"/>.</summary>
        public int DeltaContentLength => NewContentLength - OldContentLength;

        /// <summary>The time and date of the change.</summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        /// <summary>The edit/log comment.</summary>
        [JsonProperty]
        public string Comment { get; private set; }

        /// <summary>The parsed comment for the edit/log comment.</summary>
        [JsonProperty]
        public string ParsedComment { get; private set; }

        [JsonProperty]
        public IList<string> Tags { get; private set; }

        /// <summary>SHA-1 hash of the updated revision.</summary>
        [JsonProperty]
        public string Sha1 { get; private set; }

        [JsonProperty]
        public bool Redirect { get; private set; }

        /// <summary>For log items, ID of the log entry.</summary>
        [JsonProperty]
        public int? LogId { get; private set; }

        /// <summary>For log items, gets log type name.</summary>
        /// <remarks>See <see cref="LogTypes"/> for a list of predefined values.</remarks>
        [JsonProperty]
        public string LogType { get; private set; }

        /// <summary>
        /// Specific log action.
        /// </summary>
        /// <remarks>
        /// See <see cref="LogActions"/> for a list of predefined values.
        /// To determine a specific log action, you need to first check the <see cref="LogType"/>
        /// property, because certain the same log action value may have different meaning in
        /// different log type context.
        /// </remarks>
        [JsonProperty]
        public string LogAction { get; private set; }

        /// <summary>For log items, gets additional log parameters.</summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public LogParameterCollection LogParams { get; private set; } = LogParameterCollection.Empty;

        public RevisionFlags Flags { get; private set; }

        public PatrolStatus PatrolStatus { get; private set; }

        [JsonProperty] private bool Minor;
        [JsonProperty] private bool Bot;
        [JsonProperty] private bool New;
        [JsonProperty] private bool Anon;
        [JsonProperty] private bool Patrolled;
        [JsonProperty] private bool Unpatrolled;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
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
                Site.Logger.LogWarning("Patrolled and Unpatrolled are both set for rcid={Id}, page {Page}.", Id, Title);
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
                    sb.Append(string.Join(",",
                        LogParams.Select(p => p.Key + "=" + p.Value.ToString(Formatting.None))));
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
        [Obsolete]
        MoveOverRedirect,
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

}
