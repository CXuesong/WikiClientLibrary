using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents an item in RecentChanges list.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class RecentChangesEntry
    {
        public WikiSite Site { get; }

        public RecentChangesEntry(WikiSite site)
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

        public RecentChangesType Type { get; private set; }

        [JsonProperty("ns")]
        public int NamespaceId { get; private set; }

        [JsonProperty]
        public string Title { get; private set; }

        [JsonProperty]
        public int PageId { get; private set; }

        [JsonProperty("revid")]
        public int RevisionId { get; private set; }

        [JsonProperty("old_revid")]
        public int OldRevisionId { get; private set; }

        /// <summary>
        /// Id of recent change entry.
        /// </summary>
        [JsonProperty("rcid")]
        public int Id { get; private set; }

        [JsonProperty("user")]
        public string UserName { get; private set; }

        [JsonProperty("oldlen")]
        public int OldContentLength { get; private set; }

        [JsonProperty("newlen")]
        public int NewContentLength { get; private set; }

        public int DeltaContentLength => NewContentLength - OldContentLength;

        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        [JsonProperty]
        public string Comment { get; private set; }

        [JsonProperty]
        public string ParsedComment { get; private set; }

        [JsonProperty]
        public IList<string> Tags { get; private set; }

        [JsonProperty]
        public string Sha1 { get; private set; }

        [JsonProperty]
        public bool Redirect { get; private set; }

        [JsonProperty]
        public int? LogId { get; private set; }

        [JsonProperty]
        public string LogType { get; private set; }

        /// <summary>
        /// Log action.
        /// </summary>
        /// <remarks>See <see cref="LogActions"/> for a list of predefined values.</remarks>
        [JsonProperty]
        public string LogAction { get; private set; }

        // TODO Implement different classes to contain logparams for different log types.
        /// <summary>
        /// Additional log parameters.
        /// </summary>
        [JsonProperty]
        public dynamic LogParams { get; private set; }

        public RevisionFlags Flags { get; private set; }

        public PatrolStatus PatrolStatus { get; private set; }

#pragma warning disable 649
        [JsonProperty] private bool Minor;
        [JsonProperty] private bool Bot;
        [JsonProperty] private bool New;
        [JsonProperty] private bool Anon;
        [JsonProperty]
        private bool Patrolled;
        [JsonProperty]
        private bool Unpatrolled;
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
                Site.Logger?.Warn(this, $"Patrolled and Unpatrolled are both set for rcid={Id}.");
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
            push(Flags);
            push(LogAction);
            push(LogType);
            push(Title);
            push(Comment);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Predefined Log Action values.
    /// </summary>
    public static class LogActions
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
        public const string Rights = "rights";
        public const string Upload = "upload";
    }

    /// <summary>
    /// Types of recent changes. Used in <see cref="RecentChangesEntry"/>.
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
