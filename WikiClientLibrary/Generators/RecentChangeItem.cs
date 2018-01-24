using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents an item in RecentChanges list.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class RecentChangeItem
    {
        public WikiSite Site { get; }

        internal RecentChangeItem(WikiSite site)
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
                        throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid type name of change.");
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

        /// <summary>Content length of the old revision affected by this item.</summary>
        [JsonProperty("oldlen")]
        public int OldContentLength { get; private set; }

        /// <summary>Content length of the new revision affected by this item.</summary>
        [JsonProperty("newlen")]
        public int NewContentLength { get; private set; }

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
        [JsonProperty]
        public LogParameterCollection LogParams { get; private set; }

        public RevisionFlags Flags { get; private set; }

        public PatrolStatus PatrolStatus { get; private set; }

#pragma warning disable 649
        [JsonProperty] private bool Minor;
        [JsonProperty] private bool Bot;
        [JsonProperty] private bool New;
        [JsonProperty] private bool Anon;
        [JsonProperty] private bool Patrolled;
        [JsonProperty] private bool Unpatrolled;
#pragma warning restore 649

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
        /// <para>OR You don't have permission to patrol your own changes. Only users with the autopatrol right can do this.</para>
        /// </exception>
        /// <remarks>It's suggested that the caller only patrol the pages whose <see cref="PatrolStatus"/> is <see cref="Generators.PatrolStatus.Unpatrolled"/>.</remarks>
        /// <exception cref="NotSupportedException">Patrolling is disabled on this wiki.</exception>
        public Task PatrolAsync(CancellationToken cancellationToken)
        {
            if (PatrolStatus == PatrolStatus.Patrolled)
                throw new InvalidOperationException("The change has already been patrolled.");
            Site.AccountInfo.AssertRight(UserRights.Patrol);
            return RequestHelper.PatrolAsync(Site, Id, null, cancellationToken);
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            Action<object> push = v =>
            {
                if (v != null)
                {
                    sb.Append(',');
                    sb.Append(v);
                }
            };
            sb.Append(Id);
            push(TimeStamp);
            push(Type);
            sb.Append(",[");
            sb.Append(Flags);
            sb.Append("]");
            if (LogType != null)
            {
                sb.Append(',');
                sb.Append(LogType);
                sb.Append('[');
                sb.Append(LogAction);
                sb.Append(']');
                if (LogParams.Count > 0)
                {
                    sb.Append(",{");
                    sb.Append(string.Join(",", LogParams.Select(p => p.Key + "=" + p.Value.ToString(Formatting.None))));
                    sb.Append("}");
                }
            }
            push(Title);
            push(Comment);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Predefined Log Type values used in <see cref="RecentChangeItem.LogType"/>.
    /// </summary>
    /// <remarks>See <a href="https://www.mediawiki.org/wiki/Manual:Log_actions">mw:Manual:Log actions</a> for a table of typical log types.</remarks>
    public static class LogTypes
    {
        public const string Block = "block";
        public const string Delete = "delete";
        public const string Import = "import";
        public const string Merge = "merge";
        public const string Move = "move";
        public const string NewUsers = "newusers";
        public const string PageLanguage = "pagelang";
        public const string Patrol = "patrol";
        public const string Protect = "protect";
        /// <summary>Change a user's groups.</summary>
        public const string Rights = "rights";
        public const string Upload = "upload";
    }

    /// <summary>
    /// Predefined Log Action values used in <see cref="RecentChangeItem.LogAction"/>.
    /// </summary>
    /// <remarks>See <a href="https://www.mediawiki.org/wiki/Manual:Log_actions">mw:Manual:Log actions</a> for a table of typical log actions.</remarks>
    public static class LogActions
    {
        /// <summary>(<see cref="LogTypes.Block"/>) Block user.</summary>
        public const string Block = "block";
        /// <summary>(<see cref="LogTypes.Block"/>) Change block.</summary>
        public const string Reblock = "reblock";
        /// <summary>(<see cref="LogTypes.Block"/>) Unblock user.</summary>
        public const string Unblock = "unblock";
        /// <summary>(<see cref="LogTypes.Delete"/>) Delete a page.</summary>
        public const string Delete = "delete";
        /// <summary>(<see cref="LogTypes.Delete"/>) Delete a log event.</summary>
        public const string Event = "event";
        /// <summary>(<see cref="LogTypes.Delete"/>) Restore a page.</summary>
        public const string Restore = "restore";
        /// <summary>(<see cref="LogTypes.Delete"/>) Change revision visibility.</summary>
        public const string Revision = "revision";
        /// <summary>(<see cref="LogTypes.Import"/>) Import interwiki.</summary>
        public const string Interwiki = "interwiki";
        /// <summary>(<see cref="LogTypes.Merge"/>) Merge history.</summary>
        public const string Merge = "merge";
        /// <summary>(<see cref="LogTypes.Move"/>) Move a page.</summary>
        public const string Move = "move";
        /// <summary>(<see cref="LogTypes.Move"/>) Move a page over a redirect.</summary>
        public const string MoveOverRedirect = "move_redir";
        /// <summary>(<see cref="LogTypes.NewUsers"/>) When the user is automatically created (such as by CentralAuth).</summary>
        public const string AutoCreate = "autocreate";
        /// <summary>(<see cref="LogTypes.NewUsers"/>) When the created user will receive its password by email.</summary>
        public const string ByEmail = "byemail";
        /// <summary>(<see cref="LogTypes.NewUsers"/>) For an anonymous user creating an account for himself.</summary>
        public const string Create = "create";
        /// <summary>(<see cref="LogTypes.NewUsers"/>) For a logged in user creating an account for someone else.</summary>
        public const string Create2 = "create2";
        /// <summary>(<see cref="LogTypes.PageLanguage"/>) For pages whose language has been changed.</summary>
        public const string PageLanguage = "pagelang";
        /// <summary>(<see cref="LogTypes.Patrol"/>) Mark a revision as patrolled.</summary>
        public const string Patrol = "patrol";
        /// <summary>(<see cref="LogTypes.Patrol"/>) Automatic patrol of a revision.</summary>
        public const string AutoPatrol = "autopatrol";
        /// <summary>(<see cref="LogTypes.Protect"/>) Modify the protection of a protected page.</summary>
        public const string Modify = "modify";
        /// <summary>(<see cref="LogTypes.Protect"/>) Protect an unprotected page.</summary>
        public const string Protect = "protect";
        /// <summary>(<see cref="LogTypes.Protect"/>) Unprotect a page.</summary>
        public const string Unprotect = "unprotect";
        /// <summary>(<see cref="LogTypes.Rights"/>) Change a user's groups.</summary>
        public const string Rights = "rights";
        /// <summary>(<see cref="LogTypes.Upload"/>) Re-upload a file.</summary>
        public const string Overwrite = "overwrite";
        /// <summary>
        /// (<see cref="LogTypes.Import"/>) Import from an uploaded XML file.
        /// (<see cref="LogTypes.Upload"/>) Upload a new file.
        /// </summary>
        public const string Upload = "upload";
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

    /// <summary>
    /// A collection of extensible log parameters.
    /// </summary>
    /// <remarks>See <a href="https://www.mediawiki.org/wiki/Manual:Log_actions">mw:Manual:Log actions</a>
    /// for a table of typical log parameters for each type of log action.</remarks>
    public class LogParameterCollection : WikiReadOnlyDictionary
    {

        /// <summary>
        /// Namespace ID of the move target.
        /// </summary>
        public int? TargetNamespaceId => (int?)GetValueDirect("target_ns");

        /// <summary>
        /// Full title of the move target.
        /// </summary>
        public string TargetTitle => (string)GetValueDirect("target_title");

        /// <summary>
        /// Whether to suppress the creation of redirect when moving the page.
        /// </summary>
        public bool SuppressRedirect => GetValueDirect("suppressredirect") != null;

    }

}
