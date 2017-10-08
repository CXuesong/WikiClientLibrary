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

    public sealed partial class WbEntity
    {
        private static readonly WbMonolingualTextCollection emptyStringDict
            = new WbMonolingualTextCollection {IsReadOnly = true};

        private static readonly WbMonolingualTextsCollection emptyStringsDict
            = new WbMonolingualTextsCollection {IsReadOnly = true};

        private static readonly IDictionary<string, WbEntitySiteLink> emptySiteLinks
            = new ReadOnlyDictionary<string, WbEntitySiteLink>(new Dictionary<string, WbEntitySiteLink>());

        private static readonly IDictionary<string, ICollection<WbClaim>> emptyClaims
            = new ReadOnlyDictionary<string, ICollection<WbClaim>>(new Dictionary<string, ICollection<WbClaim>>());

        private ILogger logger;

        /// <summary>
        /// Initializes a new <see cref="WbEntity"/> entity from Wikibase site
        /// and entity ID.
        /// </summary>
        /// <param name="site">Wikibase site.</param>
        /// <param name="id">Entity or property ID.</param>
        public WbEntity(WikiSite site, string id)
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

        public int LastRevisionId { get; private set; }

        public WbMonolingualTextCollection Labels { get; private set; }

        public WbMonolingualTextCollection Descriptions { get; private set; }

        public WbMonolingualTextsCollection Aliases { get; private set; }

        public IDictionary<string, WbEntitySiteLink> SiteLinks { get; private set; }

        public IDictionary<string, ICollection<WbClaim>> Claims { get; private set; }

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

        private static WbMonolingualTextCollection ParseMultiLanguageValues(JObject jdict)
        {
            if (jdict == null || !jdict.HasValues) return emptyStringDict;
            return new WbMonolingualTextCollection(jdict.PropertyValues()
                    .Select(t => new WbMonolingualText((string)t["language"], (string)t["value"])))
                {IsReadOnly = true};
        }

        private static WbMonolingualTextsCollection ParseMultiLanguageMultiValues(JObject jdict)
        {
            if (jdict == null || !jdict.HasValues) return emptyStringsDict;
            return new WbMonolingualTextsCollection(jdict.Properties()
                    .Select(p => new KeyValuePair<string, IEnumerable<string>>(
                        p.Name, p.Value.Select(t => (string)t["value"]))))
                {IsReadOnly = true};
        }

        // postEditing: Is the entity param from the response of wbeditentity API call.
        internal void LoadFromJson(JToken entity, EntityQueryOptions options, bool isPostEditing)
        {
            var id = (string)entity["id"];
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
            LastRevisionId = 0;
            Labels = null;
            Aliases = null;
            Descriptions = null;
            SiteLinks = null;
            if (Exists)
            {
                Type = (string)entity["type"];
                if ((options & EntityQueryOptions.FetchInfo) == EntityQueryOptions.FetchInfo)
                {
                    if (!isPostEditing)
                    {
                        // wbeditentity response does not have these properties.
                        PageId = (int)entity["pageid"];
                        NamespaceId = (int)entity["ns"];
                        Title = (string)entity["title"];
                        LastModified = (DateTime)entity["modified"];
                    }
                    LastRevisionId = (int)entity["lastrevid"];
                }
                if ((options & EntityQueryOptions.FetchLabels) == EntityQueryOptions.FetchLabels)
                    Labels = ParseMultiLanguageValues((JObject)entity["labels"]);
                if ((options & EntityQueryOptions.FetchAliases) == EntityQueryOptions.FetchAliases)
                    Aliases = ParseMultiLanguageMultiValues((JObject)entity["aliases"]);
                if ((options & EntityQueryOptions.FetchDescriptions) == EntityQueryOptions.FetchDescriptions)
                    Descriptions = ParseMultiLanguageValues((JObject)entity["descriptions"]);
                if ((options & EntityQueryOptions.FetchSiteLinks) == EntityQueryOptions.FetchSiteLinks)
                {
                    var jlinks = (JObject)entity["sitelinks"];
                    if (jlinks == null || !jlinks.HasValues)
                    {
                        SiteLinks = emptySiteLinks;
                    }
                    else
                    {
                        SiteLinks = new ReadOnlyDictionary<string, WbEntitySiteLink>(
                            jlinks.ToObject<IDictionary<string, WbEntitySiteLink>>(Utility.WikiJsonSerializer));
                    }
                }
                if ((options & EntityQueryOptions.FetchClaims) == EntityQueryOptions.FetchClaims)
                {
                    var jclaims = (JObject)entity["claims"];
                    if (jclaims == null || !jclaims.HasValues)
                    {
                        Claims = emptyClaims;
                    }
                    else
                    {
                        // { claims : { P47 : [ {}, {}, ... ], P105 : ... } }
                        Claims = new ReadOnlyDictionary<string, ICollection<WbClaim>>(
                            jclaims.Properties().ToDictionary(p => p.Name,
                                p => (ICollection<WbClaim>)new ReadOnlyCollection<WbClaim>(
                                    p.Value.Select(WbClaim.FromJson).ToList())));
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
        /// Fetch claims on this entity.
        /// </summary>
        FetchClaims = 0x40,

        /// <summary>
        /// Fetch all the properties that is supported by WCL.
        /// </summary>
        FetchAllProperties = FetchInfo | FetchLabels | FetchAliases | FetchDescriptions | FetchSiteLinksUrl | FetchClaims,

        /// <summary>
        /// Do not resolve redirect. Treat them like deleted entities.
        /// </summary>
        SupressRedirects = 0x100,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class WbEntitySiteLink
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
