using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Get a list of all logged events, à la <c>Special:Log</c>.
    /// </summary>
    /// <remarks>This module cannot be used as a generator.</remarks>
    public class LogEventsList : WikiList<LogEventItem>
    {
        private string _LogType;
        private string _LogAction;
        private string fullLogAction;

        /// <inheritdoc />
        public LogEventsList(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        public override string ListName => "logevents";

        /// <summary>
        /// Whether to list pages in an ascending order of time. (Default: <c>false</c>)
        /// </summary>
        /// <value><c>true</c>, if oldest logs are listed first; or <c>false</c>, if newest logs are listed first.</value>
        /// <remarks>
        /// Any specified <see cref="StartTime"/> value must be later than any specified <see cref="EndTime"/> value.
        /// This requirement is reversed if <see cref="TimeAscending"/> is <c>true</c>.
        /// </remarks>
        public bool TimeAscending { get; set; } = false;

        /// <summary>
        /// The timestamp to start listing from.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The timestamp to end listing at.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Only list log events associated with pages in this namespace. (MediaWiki 1.24+)
        /// </summary>
        /// <value>Selected id of namespace, or <c>null</c> if all the namespaces are selected.</value>
        public int? NamespaceId { get; set; }

        /// <summary>
        /// Only list changes made by this user.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Only list log entries of this type.
        /// </summary>
        /// <remarks>See <see cref="LogTypes"/> for a list of predefined values.</remarks>
        public string LogType
        {
            get { return _LogType; }
            set
            {
                _LogType = value;
                fullLogAction = null;
            }
        }

        /// <summary>
        /// Filter log actions to only this type.
        /// </summary>
        /// <value>The log action name. When <see cref="LogType"/> is <c>null</c>,
        /// this is the log type name and action name, such as <c>block/block</c>, <c>block/unblock</c>;
        /// otherwise, this is only the action name <c>block</c>, <c>unblock</c>,
        /// and <see cref="LogType"/> will be prepended to the action name automatically.</value>
        /// <remarks>
        /// <para>See <see cref="LogActions"/> for a list of predefined values.</para>
        /// <para>
        /// Note that for MediaWiki 1.19 and before, using the same action name as log type name is not allowed.
        /// For example, you should set <see cref="LogType"/> to <c>"move"</c>,
        /// and <see cref="LogAction"/> to <c>null</c> instead of <c>"move"</c>,
        /// or <c>"move/move"</c>, to filter in all the page move events.
        /// </para>
        /// </remarks>
        public string LogAction
        {
            get { return _LogAction; }
            set
            {
                _LogAction = value;
                fullLogAction = null;
            }
        }

        /// <summary>
        /// Only list event entries tagged with this tag.
        /// </summary>
        public string Tag { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            if (fullLogAction == null && LogAction != null)
            {
                if (LogType != null) fullLogAction = LogType + "/" + LogAction;
                else fullLogAction = LogAction;
            }

            return new Dictionary<string, object>
            {
                {"leprop", "user|userid|comment|parsedcomment|timestamp|title|ids|details|type|tags"},
                {"ledir", TimeAscending ? "newer" : "older"},
                {"lestart", StartTime},
                {"leend", EndTime},
                {"lenamespace", NamespaceId},
                {"leuser", UserName},
                {"letag", Tag},
                {"letype", LogType},
                {"leaction", fullLogAction},
                {"lelimit", PaginationSize}
            };
        }

        // Maps the legacy MW log event parameter names into names as presented in `params` node.
        private static readonly Dictionary<string, string> legacyLogEventParamNameMapping = new Dictionary<string, string>
        {
            {"new_ns", "target_ns"},
            {"new_title", "target_title"},
        };

        /// <inheritdoc />
        protected override LogEventItem ItemFromJson(JToken json)
        {
            if (json["params"] == null)
            {
                // Can be legacy log event format (as in MW 1.19),
                // Need to fix it.
                // Fist, check if there are suspectable legacy "params" node
                var type = (string)json["type"];
                var joldParams = (JObject)json[type];
                if (type != null && joldParams != null)
                {
                    var jparams = new JObject();
                    foreach (var prop in joldParams.Properties())
                    {
                        // Detach
                        var value = prop.Value;
                        prop.Value = null;
                        if (legacyLogEventParamNameMapping.TryGetValue(prop.Name, out var mappedName))
                            jparams[mappedName] = value;
                        else
                            jparams[prop.Name] = value;
                    }

                    json["params"] = jparams;
                }
            }

            return json.ToObject<LogEventItem>(Utility.WikiJsonSerializer);
        }
    }

    /// <summary>
    /// Represents an MediaWiki log event entry.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class LogEventItem
    {

        [JsonConstructor]
        private LogEventItem()
        {

        }

        internal static LogEventItem FromRecentChangeItem(RecentChangeItem rc)
        {
            Debug.Assert(rc?.LogType != null);
            return new LogEventItem
            {
                Type = rc.LogType,
                Action = rc.LogAction,
                Params = rc.LogParams,
                TimeStamp = rc.TimeStamp,
                LogId = (int)rc.LogId,
                UserId = rc.UserId,
                UserName = rc.UserName,
                Comment = rc.Comment,
                NamespaceId = rc.NamespaceId,
                PageId = rc.PageId,
                ParsedComment = rc.ParsedComment,
                Tags = rc.Tags,
                Title = rc.Title
            };
        }

        /// <summary>Namespace ID of the page affected by this item.</summary>
        [JsonProperty("ns")]
        public int NamespaceId { get; private set; }

        /// <summary>Full title of the page affected by this item.</summary>
        /// <remarks>For user operation, this is the title user page of target user.</remarks>
        [JsonProperty]
        public string Title { get; private set; }
        
        /// <summary>the page id at the time the log was stored.</summary>
        [JsonProperty("logpage")]
        public int PageId { get; private set; }

        /// <summary>Name of the user making this recent change.</summary>
        [JsonProperty("user")]
        public string UserName { get; private set; }

        /// <summary>The user ID who was responsible for the log event/recent change.</summary>
        /// <remarks>
        /// <para>When <c>userid</c> property is specified in the MediaWiki API request,
        /// for account creation events, this is user ID of the creating user is returned.
        /// When absent, this is the user ID returned is that of the created account
        /// (see <a href="https://phabricator.wikimedia.org/T73020">phab:T73020</a>).
        /// In most cases (such as in <see cref="LogEventsList"/> or <see cref="RecentChangesGenerator"/>,
        /// <c>userid</c> property is specified implicitly.</para>
        /// <para>To get the user ID for the created user, especially in <see cref="LogActions.Create2"/> log action,
        /// use <see cref="Params"/>.<see cref="LogParameterCollection.UserId"/> .</para>
        /// </remarks>
        [JsonProperty]
        public int UserId { get; private set; }

        /// <summary>The time and date of the change.</summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        /// <summary>The edit/log comment.</summary>
        [JsonProperty]
        public string Comment { get; private set; }

        /// <summary>The parsed comment for the edit/log comment.</summary>
        [JsonProperty]
        public string ParsedComment { get; private set; }

        /// <summary>Tags for the event.</summary>
        [JsonProperty]
        public IList<string> Tags { get; private set; }

        /// <summary>Gets ID of the log entry.</summary>
        [JsonProperty]
        public int LogId { get; private set; }

        /// <summary>Gets log type name.</summary>
        /// <remarks>See <see cref="LogTypes"/> for a list of predefined values.</remarks>
        [JsonProperty]
        public string Type { get; private set; }

        /// <summary>
        /// Specific log action.
        /// </summary>
        /// <remarks>
        /// See <see cref="LogActions"/> for a list of predefined values.
        /// To determine a specific log action, you need to first check the <see cref="Type"/>
        /// property, because certain the same log action value may have different meaning in
        /// different log type context.
        /// </remarks>
        [JsonProperty]
        public string Action { get; private set; }

        /// <summary>For log items, gets additional log parameters.</summary>
        /// <value>
        /// The without additional parameters of the log item.
        /// For log items without additional parameters available,
        /// this is an empty collection.
        /// </value>
        /// <remarks>
        /// For modern MediaWiki builds, this property uses the value of `params` property.
        /// For compatibility with MediaWiki 1.19 and below, this property also tries to use the property
        /// whose name is the value of <see cref="Type"/>. (e.g. use `move` property if <see cref="Type"/> is <see cref="LogActions.Move"/>.
        /// </remarks>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public LogParameterCollection Params { get; private set; } = LogParameterCollection.Empty;

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(LogId);
            sb.Append(',');
            sb.Append(TimeStamp);
            sb.Append(',');
            sb.Append(Type);
            if (Type != Action)
            {
                sb.Append('/');
                sb.Append(Action);
            }
            sb.Append(',');
            sb.Append(Title);
            sb.Append(",{");
            if (Params.Count > 0)
            {
                sb.Append(string.Join(",", Params.Select(p => p.Key + "=" + p.Value.ToString(Formatting.None))));
            }
            sb.Append("},");
            sb.Append(UserName);
            return sb.ToString();
        }
    }


    /// <summary>
    /// A collection of extensible log parameters.
    /// </summary>
    /// <remarks>See <a href="https://www.mediawiki.org/wiki/Manual:Log_actions">mw:Manual:Log actions</a>
    /// for a table of typical log parameters for each type of log action.</remarks>
    public class LogParameterCollection : WikiReadOnlyDictionary
    {

        internal static readonly LogParameterCollection Empty = new LogParameterCollection();

        static LogParameterCollection()
        {
            Empty.MakeReadonly();
        }

        /// <summary>
        /// (<see cref="LogActions.Move"/>) Namespace ID of the move target.
        /// </summary>
        public int TargetNamespaceId => GetInt32Value("target_ns");

        /// <summary>
        /// (<see cref="LogActions.Move"/>) Full title of the move target.
        /// </summary>
        public string TargetTitle => GetStringValue("target_title");

        /// <summary>
        /// (<see cref="LogActions.Move"/>) Whether to suppress the creation of redirect when moving the page.
        /// </summary>
        /// <remarks>
        /// This property returns true if either <c>suppressredirect</c> (Newer MediaWiki)
        /// or <c>suppressedredirect</c> (MediaWiki 1.19, or Wikia)
        /// is specified as true in the parameter collection.
        /// </remarks>
        public bool SuppressRedirect => GetBooleanValue("suppressredirect")
                                        || GetBooleanValue("suppressedredirect");   // Yes, this one is dedicated to Wikia.

        /// <summary>
        /// (<see cref="LogActions.Patrol"/>)
        /// </summary>
        public int CurrentRevisionId => GetInt32Value("curid", 0);

        /// <summary>
        /// (<see cref="LogActions.Patrol"/>)
        /// </summary>
        public int PreviousRevisionId => GetInt32Value("previd", 0);

        /// <summary>
        /// (<see cref="LogActions.Patrol"/>)
        /// </summary>
        public bool IsAutoPatrol => GetBooleanValue("auto");

        /// <summary>
        /// (<see cref="LogTypes.NewUsers"/>) The user ID of the created user.
        /// </summary>
        /// <see cref="LogEventItem.UserId"/>
        public int UserId => GetInt32Value("userid", 0);

    }

    /// <summary>
    /// Predefined Log Type values used in <see cref="LogEventItem.Type"/>.
    /// </summary>
    /// <remarks>
    /// See <a href="https://www.mediawiki.org/wiki/Manual:Log_actions">mw:Manual:Log actions</a> for a table of typical log types.
    /// Note that extensions may add other log types.
    /// </remarks>
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
    /// Predefined Log Action values used in <see cref="LogEventItem.Action"/>.
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
        /// <summary>(<see cref="LogTypes.Move"/>) Move a page over a redirect. (N/A to MediaWiki 1.19)</summary>
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


}
