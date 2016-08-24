using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// Only list changes tagged with this tag.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Only list certain types of changes.
        /// </summary>
        public RecentChangesTypes ChangesTypes { get; set; } = RecentChangesTypes.All;

        public PropertyFilterOption MinorFilter { get; set; }

        public PropertyFilterOption BotFilter { get; set; }

        public PropertyFilterOption AnnonymousFilter { get; set; }

        public PropertyFilterOption RedirectsFilter { get; set; }

        public PropertyFilterOption PatrolledFilter { get; set; }

        /// <summary>
        /// Only list changes which are the latest revision.
        /// </summary>
        public bool LastRevisionsOnly { get; set; }


        private string ParseRecentChangesTypes(RecentChangesTypes value)
        {
            var types = "";
            if ((value & RecentChangesTypes.Edit) == RecentChangesTypes.Edit) types += "|edit";
            if ((value & RecentChangesTypes.ExternalEdit) == RecentChangesTypes.ExternalEdit) types += "|external";
            if ((value & RecentChangesTypes.Creation) == RecentChangesTypes.Creation) types += "|new";
            if ((value & RecentChangesTypes.LogEntry) == RecentChangesTypes.LogEntry) types += "|log";
            if (types.Length == 0) throw new ArgumentOutOfRangeException(nameof(value));
            return types.Substring(1);
        }

        private string ParseFilters()
        {
            var types = MinorFilter.ToString("|minor", "|!minor", "")
                        + BotFilter.ToString("|bot", "|!bot", "")
                        + AnnonymousFilter.ToString("|anon", "|!anon", "")
                        + RedirectsFilter.ToString("|redirect", "|!redirect", "")
                        + PatrolledFilter.ToString("|patrolled", "|!patrolled", "");
            return types;
        }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containning request value pairs.</returns>
        protected override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams()
        {
            return new Dictionary<string, object>
            {
                {"generator", "recentchanges"},
                {"grcdir", TimeAscending ? "newer" : "older"},
                {"grcstart", StartTime},
                {"grcend", EndTime},
                {"grcnamespace", NamespaceIds == null ? null : string.Join("|", NamespaceIds)},
                {"grcuser", UserName},
                {"grctag", Tag},
                {"grctype", ParseRecentChangesTypes(ChangesTypes)},
                {"grcshow", ParseFilters()},
                {"grctoponly", LastRevisionsOnly},
                {"grclimit", ActualPagingSize},
            };
        }
    }

    /// <summary>
    /// Types of recent changes.
    /// </summary>
    [Flags]
    public enum RecentChangesTypes
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
        /// External edits.
        /// </summary>
        ExternalEdit = 2,

        /// <summary>
        /// Page creations (Uploads are not listed as Creation but as LogEntry).
        /// </summary>
        Creation = 4,

        /// <summary>
        /// Log entries.
        /// </summary>
        LogEntry = 8,

        /// <summary>
        /// All types of changes.
        /// </summary>
        All = Edit | ExternalEdit | Creation | LogEntry,
    }
}
