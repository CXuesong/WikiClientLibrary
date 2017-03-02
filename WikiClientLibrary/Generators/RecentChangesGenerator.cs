using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Get all recent changes to the wiki, à la Special:Recentchanges.
    /// </summary>
    public class RecentChangesGenerator : PageGenerator<Page>
    {
        public RecentChangesGenerator(Site site) : base(site)
        {
        }

        /// <summary>
        /// Whether to list pages in an ascending order of time.
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

        public PropertyFilterOption MinorFilter { get; set; }

        public PropertyFilterOption BotFilter { get; set; }

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

        private IEnumerable<KeyValuePair<string, object>> GetParams(bool asList, int actualPagingSize)
        {
            var dict = new Dictionary<string, object>
            {
                {asList ? "list" : "generator", "recentchanges"}
            };
            Action<string, object> addParam = (k, v) => dict.Add(asList ? k : ("g" + k), v);
            addParam("rcdir", TimeAscending ? "newer" : "older");
            addParam("rcstart", StartTime);
            addParam("rcend", EndTime);
            addParam("rcnamespace", NamespaceIds == null ? null : string.Join("|", NamespaceIds));
            addParam("rcuser", UserName);
            addParam("rcexcludeuser", ExcludedUserName);
            addParam("rctag", Tag);
            addParam("rctype", ParseRecentChangesTypes(TypeFilters));
            addParam("rcshow", ParseFilters());
            addParam("rctoponly", LastRevisionsOnly);
            addParam("rclimit", actualPagingSize);
            if (asList)
            {
                // All except userid .
                // rcpermissiondenied
                var fields = "user|comment|parsedcomment|flags|timestamp|title|ids|sizes|redirect|loginfo|tags|sha1";
                if (Site.AccountInfo.HasRight(UserRights.Patrol)) fields += "|patrolled";
                addParam("rcprop", fields);
            }
            return dict;
        }

        // Duplicate results can be shown among continued query results in recent changes,
        // if a wiki page is modified more than once. And when a page title is shown for the
        // 2nd, 3rd, etc time, only the properties that has been changed will be included in JSON,
        // which will screw up Page.LoadFromJson .
        protected override bool DistinctGeneratedPages => true;

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <param name="actualPagingSize"></param>
        /// <returns>The dictioanry containing request value pairs.</returns>
        protected override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams(int actualPagingSize)
        {
            return GetParams(false, actualPagingSize);
        }

        /// <summary>
        /// Asynchronously generate a sequence of <see cref="RecentChangesEntry"/>.
        /// </summary>
        public IAsyncEnumerable<RecentChangesEntry> EnumRecentChangesAsync()
        {
            var valuesDict = new Dictionary<string, object>
            {
                {"action", "query"},
                {"maxlag", 5}
            };
            foreach (var v in GetParams(true, GetActualPagingSize(PageQueryOptions.None)))
                valuesDict[v.Key] = v.Value;
            Debug.Assert((string) valuesDict["action"] == "query");
            var paging = new PagedQueryAsyncEnumerable(Site, valuesDict);
            var serializer = Utility.CreateWikiJsonSerializer();
            serializer.Converters.Insert(0, new RcEntryCreator(Site));
            return paging.SelectMany(jquery => jquery["recentchanges"]
                .ToObject<IList<RecentChangesEntry>>(serializer).ToAsyncEnumerable());
        }

        /// <summary>
        /// Generate a sequence of <see cref="RecentChangesEntry"/>.
        /// </summary>
        public IEnumerable<RecentChangesEntry> EnumRecentChanges()
        {
            return EnumRecentChangesAsync().ToEnumerable();
        }

        private class RcEntryCreator : CustomCreationConverter<RecentChangesEntry>
        {
            public RcEntryCreator(Site site)
            {
                if (site == null) throw new ArgumentNullException(nameof(site));
                Site = site;
            }

            public Site Site { get; }

            public override RecentChangesEntry Create(Type objectType)
            {
                return new RecentChangesEntry(Site);
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
