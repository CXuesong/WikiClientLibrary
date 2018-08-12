using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates all recent changes to the wiki, à la Special:Recentchanges.
    /// </summary>
    public class RecentChangesGenerator : WikiPageGenerator<RecentChangeItem>
    {

        /// <inheritdoc />
        public RecentChangesGenerator(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// Whether to list pages in an ascending order of time.  (Default: <c>false</c>)
        /// </summary>
        /// <value><c>true</c>, if oldest changes are listed first; or <c>false</c>, if newest changes are listed first.</value>
        /// <remarks>
        /// Any specified <see cref="StartTime"/> value must be later than any specified <see cref="EndTime"/> value.
        /// This requirement is reversed if <see cref="TimeAscending"/> is <c>true</c>.
        /// </remarks>
        public bool TimeAscending { get; set; } = false;

        /// <summary>
        /// The timestamp to start listing from.
        /// (May not be more than $wgRCMaxAge into the past, which on Wikimedia wikis is 30 days.)
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// The timestamp to end listing at.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>Selected ids of namespace, or null if all the namespaces are selected.</value>
        public IEnumerable<int> NamespaceIds { get; set; }

        /// <summary>
        /// Only list changes made by this user.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Do not list changes made by this user.
        /// </summary>
        public string ExcludedUserName { get; set; }

        /// <summary>
        /// Only list changes tagged with this tag.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Only list certain types of changes.
        /// </summary>
        public RecentChangesFilterTypes TypeFilters { get; set; } = RecentChangesFilterTypes.All;

        /// <summary>
        /// Whether to list minor edits.
        /// </summary>
        public PropertyFilterOption MinorFilter { get; set; }

        /// <summary>
        /// Whether to list bot edits.
        /// </summary>
        public PropertyFilterOption BotFilter { get; set; }

        /// <summary>
        /// Whether to list edits by anonymous users.
        /// </summary>
        public PropertyFilterOption AnonymousFilter { get; set; }

        /// <summary>
        /// Whether to list edits to pages that are currently redirects.
        /// </summary>
        public PropertyFilterOption RedirectsFilter { get; set; }

        /// <summary>
        /// Whether to list edits flagged as patrolled. Only available to users with the patrol right.
        /// </summary>
        public PropertyFilterOption PatrolledFilter { get; set; }

        /// <summary>
        /// Only list changes which are the latest revision.
        /// </summary>
        /// <remarks>
        /// This WILL NOT make the results distinct. This option seems only affets EDIT changes.
        /// </remarks>
        public bool LastRevisionsOnly { get; set; }

        private string ParseRecentChangesTypes(RecentChangesFilterTypes value)
        {
            var types = "";
            if ((value & RecentChangesFilterTypes.Edit) == RecentChangesFilterTypes.Edit) types += "|edit";
            if ((value & RecentChangesFilterTypes.External) == RecentChangesFilterTypes.External) types += "|external";
            if ((value & RecentChangesFilterTypes.Create) == RecentChangesFilterTypes.Create) types += "|new";
            if ((value & RecentChangesFilterTypes.Log) == RecentChangesFilterTypes.Log) types += "|log";
            if ((value & RecentChangesFilterTypes.Categorize) == RecentChangesFilterTypes.Log) types += "|categorize";
            if (types.Length == 0) throw new ArgumentOutOfRangeException(nameof(value));
            return types.Substring(1);
        }

        private string ParseFilters()
        {
            var types = MinorFilter.ToString("|minor", "|!minor", "")
                        + BotFilter.ToString("|bot", "|!bot", "")
                        + AnonymousFilter.ToString("|anon", "|!anon", "")
                        + RedirectsFilter.ToString("|redirect", "|!redirect", "")
                        + PatrolledFilter.ToString("|patrolled", "|!patrolled", "");
            return types.Length > 1 ? types.Substring(1) : null;
        }

        private IEnumerable<KeyValuePair<string, object>> EnumParams(bool isList)
        {
            var prefix = isList ? null : "g";
            var dict = new Dictionary<string, object>
            {
                {prefix + "rcdir", TimeAscending ? "newer" : "older"},
                {prefix + "rcstart", StartTime},
                {prefix + "rcend", EndTime},
                {prefix + "rcnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds)},
                {prefix + "rcuser", UserName},
                {prefix + "rcexcludeuser", ExcludedUserName},
                {prefix + "rctag", Tag},
                {prefix + "rctype", ParseRecentChangesTypes(TypeFilters)},
                {prefix + "rcshow", ParseFilters()},
                {prefix + "rctoponly", LastRevisionsOnly},
                {prefix + "rclimit", PaginationSize}
            };
            if (isList)
            {
                var fields = "user|userid|comment|parsedcomment|flags|timestamp|title|ids|sizes|redirect|loginfo|tags|sha1";
                if (Site.AccountInfo.HasRight(UserRights.Patrol)) fields += "|patrolled";
                dict.Add("rcprop", fields);
            }
            return dict;
        }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            return EnumParams(true);
        }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumGeneratorParameters()
        {
            return EnumParams(false);
        }

        // Duplicate results can be shown among continued query results in recent changes,
        // if a wiki page is modified more than once. And when a page title is shown for the
        // 2nd, 3rd, etc time, only the properties that has been changed will be included in JSON,
        // which will screw up Page.LoadFromJson .
        /// <inheritdoc />
        protected override bool DistinctGeneratedPages => true;

        /// <inheritdoc />
        public override string ListName => "recentchanges";

        private JsonSerializer rcitemSerializer;

        /// <inheritdoc />
        protected override RecentChangeItem ItemFromJson(JToken json)
        {
            var serializer = rcitemSerializer;
            if (serializer == null)
            {
                serializer = Utility.CreateWikiJsonSerializer();
                serializer.Converters.Insert(0, new RcEntryCreator(Site));
                Volatile.Write(ref rcitemSerializer, serializer);
            }
            return json.ToObject<RecentChangeItem>(serializer);
        }

        private class RcEntryCreator : CustomCreationConverter<RecentChangeItem>
        {
            public RcEntryCreator(WikiSite site)
            {
                if (site == null) throw new ArgumentNullException(nameof(site));
                Site = site;
            }

            public WikiSite Site { get; }

            public override RecentChangeItem Create(Type objectType)
            {
                return new RecentChangeItem(Site);
            }
        }
    }

    /// <summary>
    /// Types of recent changes. Used in <see cref="RecentChangesGenerator"/>.
    /// </summary>
    [Flags]
    public enum RecentChangesFilterTypes
    {
        /// <summary>
        /// Invalid enum value. Using this value may cause exceptions.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Regular page edits.
        /// </summary>
        Edit = 1,

        /// <summary>
        /// An external recent change. Primarily used by Wikidata.
        /// </summary>
        External = 2,

        /// <summary>
        /// Page creations (Uploads are not listed as Creation but as LogEntry).
        /// Using this flag can create a New Page Generator.
        /// </summary>
        Create = 4,

        /// <summary>
        /// Log entries.
        /// </summary>
        Log = 8,

        /// <summary>
        /// Category membership change. (MediaWiki 1.27)
        /// </summary>
        Categorize = 16,

        /// <summary>
        /// All types of changes.
        /// </summary>
        All = Edit | External | Create | Log | Categorize
    }
}
