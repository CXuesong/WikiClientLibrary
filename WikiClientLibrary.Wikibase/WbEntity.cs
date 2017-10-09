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
using WikiClientLibrary.Wikibase.Infrastructures;

namespace WikiClientLibrary.Wikibase
{

    public sealed partial class WbEntity : IWikiClientLoggable
    {
        private static readonly WbMonolingualTextCollection emptyStringDict
            = new WbMonolingualTextCollection {IsReadOnly = true};

        private static readonly WbMonolingualTextsCollection emptyStringsDict
            = new WbMonolingualTextsCollection {IsReadOnly = true};

        private static readonly WbEntitySiteLinkCollection emptySiteLinks
            = new WbEntitySiteLinkCollection {IsReadOnly = true};

        private static readonly WbClaimCollection emptyClaims
            = new WbClaimCollection {IsReadOnly = true};

        private ILoggerFactory _LoggerFactory;

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

        protected ILogger Logger { get; private set; }

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

        public WbEntitySiteLinkCollection SiteLinks { get; private set; }

        public WbClaimCollection Claims { get; private set; }

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
                        SiteLinks = WbEntitySiteLinkCollection.Create(
                            jlinks.Values().Select(t => t.ToObject<WbEntitySiteLink>(Utility.WikiJsonSerializer)));
                        SiteLinks.IsReadOnly = true;
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
                        Claims = WbClaimCollection.Create(jclaims.Values()
                            .SelectMany(jarray => jarray.Select(WbClaim.FromJson)));
                    }
                }
            }
            QueryOptions = options;
        }

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => Logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
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
    
    public sealed class WbEntitySiteLink
    {
        
        public WbEntitySiteLink(string site, string title) : this(site, title, null, null)
        {
        }
        
        public WbEntitySiteLink(string site, string title, IList<string> badges) : this(site, title, badges, null)
        {
        }

        [JsonConstructor]
        public WbEntitySiteLink(string site, string title, IList<string> badges, string url)
        {
            Site = site;
            Title = title;
            Badges = badges;
            if (!badges.IsReadOnly) Badges = new ReadOnlyCollection<string>(badges);
            Url = url;
        }

        public string Site { get; }
        
        public string Title { get; }
        
        public IList<string> Badges { get; }
        
        public string Url { get; }

    }

    public class WbEntitySiteLinkCollection : UnorderedKeyedCollection<string, WbEntitySiteLink>
    {
        internal static WbEntitySiteLinkCollection Create(IEnumerable<WbEntitySiteLink> items)
        {
            Debug.Assert(items != null);
            var inst = new WbEntitySiteLinkCollection();
            foreach (var i in items) inst.Add(i);
            return inst;
        }

        /// <inheritdoc />
        protected override string GetKeyForItem(WbEntitySiteLink item)
        {
            return item.Site;
        }
    }

    public class WbClaimCollection : UnorderedKeyedMultiCollection<string, WbClaim>
    {

        internal static WbClaimCollection Create(IEnumerable<WbClaim> items)
        {
            Debug.Assert(items != null);
            var inst = new WbClaimCollection();
            foreach (var i in items) inst.Add(i);
            return inst;
        }

        /// <inheritdoc />
        protected override string GetKeyForItem(WbClaim item)
        {
            return item.MainSnak?.PropertyId;
        }

        /// <inheritdoc />
        public override void Add(WbClaim item)
        {
            base.Add(item);
            item.KeyChanging += Item_KeyChanging;
        }

        /// <inheritdoc />
        public override bool Remove(string key)
        {
            AssertMutable();
            foreach (var item in this[key])
                item.KeyChanging -= Item_KeyChanging;
            return base.Remove(key);
        }

        /// <inheritdoc />
        public override bool Remove(WbClaim item)
        {
            if (base.Remove(item))
            {
                item.KeyChanging -= Item_KeyChanging;
                return true;
            }
            return false;
        }

        private void Item_KeyChanging(object sender, KeyChangingEventArgs e)
        {
            ChangeItemKey((WbClaim)sender, (string)e.NewKey);
        }
    }

}
