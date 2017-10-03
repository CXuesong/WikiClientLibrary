using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using System.Threading;

namespace WikiClientLibrary.Wikibase
{

    public sealed class WikibaseEntity
    {
        private static readonly IDictionary<string, string> emptyStringDict =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private static readonly IDictionary<string, ICollection<string>> emptyStringsDict =
            new ReadOnlyDictionary<string, ICollection<string>>(new Dictionary<string, ICollection<string>>());

        private static readonly IDictionary<string, EntitySiteLink> emptySiteLinks =
            new ReadOnlyDictionary<string, EntitySiteLink>(new Dictionary<string, EntitySiteLink>());

        private ILogger logger;

        public WikibaseEntity(WikiSite site, string id)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public WikiSite Site { get; }

        public string Id { get; private set; }

        public int PageId { get; private set; }

        public int NamespaceId { get; private set; }

        public string Title { get; private set; }

        public string Type { get; private set; }

        public bool Exists { get; private set; }

        public DateTime LastModified { get; private set; }

        public IDictionary<string, string> Labels { get; private set; }

        public IDictionary<string, string> Descriptions { get; private set; }

        public IDictionary<string, ICollection<string>> Aliases { get; private set; }

        public IDictionary<string, EntitySiteLink> SiteLinks { get; private set; }

        public EntityQueryOptions QueryOptions { get; private set; }

        public Task RefreshAsync()
        {
            return RefreshAsync(EntityQueryOptions.None, null, CancellationToken.None);
        }

        public Task RefreshAsync(EntityQueryOptions options)
        {
            return RefreshAsync(options, null, CancellationToken.None);
        }

        public Task RefreshAsync(EntityQueryOptions options, ICollection<string> languages)
        {
            return RefreshAsync(options, languages, CancellationToken.None);
        }

        public Task RefreshAsync(EntityQueryOptions options, ICollection<string> languages, CancellationToken cancellationToken)
        {
            return WikibaseRequestHelper.RefreshEntitiesAsync(new[] {this}, options, languages, cancellationToken);
        }

        private static IDictionary<string, string> ParseMultiLanguageValues(JObject jdict)
        {
            if (jdict == null || !jdict.HasValues) return emptyStringDict;
            return new ReadOnlyDictionary<string, string>(jdict.PropertyValues()
                .ToDictionary(t => (string) t["language"], t => (string) t["value"], StringComparer.OrdinalIgnoreCase));
        }

        private static IDictionary<string, ICollection<string>> ParseMultiLanguageMultiValues(JObject jdict)
        {
            if (jdict == null || !jdict.HasValues) return emptyStringsDict;
            return new ReadOnlyDictionary<string, ICollection<string>>(jdict.Properties()
                .ToDictionary(p => p.Name, p => (ICollection<string>) new ReadOnlyCollection<string>(
                    p.Value.Select(t => (string) t["value"]).ToList()), StringComparer.OrdinalIgnoreCase));
        }

        internal void LoadFromJson(JToken entity, EntityQueryOptions options)
        {
            var id = (string) entity["id"];
            Debug.Assert(id != null);
            if ((options & EntityQueryOptions.SupressRedirects) != EntityQueryOptions.SupressRedirects
                && Id != null && Id != id)
            {
                // The page has been overwritten, or deleted.
                //logger.LogWarning("Detected change of page id for [[{Title}]]: {Id1} -> {Id2}.", Title, Id, id);
            }
            Id = id;
            Exists = entity["missing"] == null;
            Type = null;
            PageId = -1;
            NamespaceId = -1;
            Title = null;
            LastModified = DateTime.MinValue;
            Labels = null;
            Aliases = null;
            Descriptions = null;
            SiteLinks = null;
            if (Exists)
            {
                Type = (string) entity["type"];
                if ((options & EntityQueryOptions.FetchInfo) == EntityQueryOptions.FetchInfo)
                {
                    PageId = (int) entity["pageid"];
                    NamespaceId = (int) entity["ns"];
                    Title = (string) entity["title"];
                    LastModified = (DateTime) entity["modified"];
                }
                if ((options & EntityQueryOptions.FetchLabels) == EntityQueryOptions.FetchLabels)
                    Labels = ParseMultiLanguageValues((JObject) entity["labels"]);
                if ((options & EntityQueryOptions.FetchAliases) == EntityQueryOptions.FetchAliases)
                    Aliases = ParseMultiLanguageMultiValues((JObject) entity["aliases"]);
                if ((options & EntityQueryOptions.FetchDescriptions) == EntityQueryOptions.FetchDescriptions)
                    Descriptions = ParseMultiLanguageValues((JObject) entity["descriptions"]);
                if ((options & EntityQueryOptions.FetchSiteLinks) == EntityQueryOptions.FetchSiteLinks)
                {
                    var jlinks = (JObject) entity["sitelinks"];
                    if (jlinks == null || !jlinks.HasValues)
                    {
                        SiteLinks = emptySiteLinks;
                    }
                    else
                    {
                        SiteLinks = new ReadOnlyDictionary<string, EntitySiteLink>(
                            jlinks.ToObject<IDictionary<string, EntitySiteLink>>(Utility.WikiJsonSerializer));
                    }
                }
            }
            QueryOptions = options;
        }

    }

    [Flags]
    public enum EntityQueryOptions
    {
        None = 0,
        FetchInfo = 1,
        FetchLabels = 2,
        FetchAliases = 4,
        FetchDescriptions = 8,
        /// <summary>
        /// Fetch associated wiki site links.
        /// </summary> 
        FetchSiteLinks = 0x10,
        /// <summary>
        /// Fetch associated wiki site links, along with link URLs.
        /// This option implies <see cref="FetchSiteLinks"/>.
        /// </summary>
        FetchSiteLinksUrl = 0x20 | FetchSiteLinks,

        /// <summary>
        /// Fetch all the properties that is supported by WCL.
        /// </summary>
        FetchAllProperties = FetchInfo | FetchLabels | FetchAliases | FetchDescriptions | FetchSiteLinks,

        /// <summary>
        /// Do not resolve redirect. Treat them like deleted entities.
        /// </summary>
        SupressRedirects = 0x100,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EntitySiteLink
    {

        [JsonProperty]
        public string Site { get; private set; }

        [JsonProperty]
        public string Title { get; private set; }

        [JsonProperty]
        public IList<string> Badges { get; private set; }

        [JsonProperty]
        public string Url { get; private set; }

    }

}
