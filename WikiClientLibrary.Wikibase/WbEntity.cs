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
using Microsoft.Extensions.Logging.Abstractions;
using WikiClientLibrary.Wikibase.Infrastructures;

namespace WikiClientLibrary.Wikibase
{

    /// <summary>
    /// Provides basic information on a Wikibase item or property.
    /// </summary>
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
        /// Initializes a new <see cref="WbEntity"/> entity from Wikibase site,
        /// marked for creation.
        /// </summary>
        /// <param name="site">Wikibase site.</param>
        /// <param name="type">Type of the new entity.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is neither
        /// <see cref="WbEntityType.Item"/> nor <see cref="WbEntityType.Property"/>.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> is <c>null</c>.</exception>
        public WbEntity(WikiSite site, WbEntityType type)
        {
            if (type != WbEntityType.Item && type != WbEntityType.Property)
                throw new ArgumentOutOfRangeException(nameof(type));
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Id = null;
            Type = type;
            LoggerFactory = site.LoggerFactory;
        }

        /// <summary>
        /// Initializes a new <see cref="WbEntity"/> entity from Wikibase site
        /// and existing entity ID.
        /// </summary>
        /// <param name="site">Wikibase site.</param>
        /// <param name="id">Entity or property ID, without <c>Property:</c> prefix.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="id"/> is <c>null</c>.</exception>
        public WbEntity(WikiSite site, string id)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public WikiSite Site { get; }

        private ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Id of the entity.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// ID of the entity page.
        /// </summary>
        /// <remarks>
        /// The property value is invalidated after you have performed edits on this instance.
        /// To fetch the latest value, use <see cref="RefreshAsync(EntityQueryOptions)"/>.
        /// </remarks>
        public int PageId { get; private set; }

        /// <summary>
        /// Namespace ID of the entity page.
        /// </summary>
        public int NamespaceId { get; private set; }

        /// <summary>
        /// Full title of the entity page.
        /// </summary>
        /// <remarks><para>For items, they are usually in the form of <c>Q1234</c>;
        /// for properties, they are usually in the form of <c>Property:P1234</c>.</para>
        /// <para>The property value is invalidated after you have performed edits on this instance.
        /// To fetch the latest value, use <see cref="RefreshAsync(EntityQueryOptions)"/>.</para>
        /// </remarks>
        public string Title { get; private set; }

        /// <summary>
        /// Wikibase entity type.
        /// </summary>
        public WbEntityType Type { get; private set; }

        /// <summary>
        /// For property entity, gets the data type of the property.
        /// </summary>
        public WbPropertyType DataType { get; private set; }

        /// <summary>
        /// Whether the entity exists.
        /// </summary>
        public bool Exists { get; private set; }

        /// <summary>Time of the last revision.</summary>
        /// <remarks>
        /// The property value is invalidated after you have performed edits on this instance.
        /// To fetch the latest value, use <see cref="RefreshAsync(EntityQueryOptions)"/>.
        /// </remarks>
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// The revid of the last revision.
        /// </summary>
        public int LastRevisionId { get; private set; }

        public WbMonolingualTextCollection Labels { get; private set; }

        public WbMonolingualTextCollection Descriptions { get; private set; }

        public WbMonolingualTextsCollection Aliases { get; private set; }

        public WbEntitySiteLinkCollection SiteLinks { get; private set; }

        public WbClaimCollection Claims { get; private set; }

        /// <summary>
        /// The last query options used with <see cref="RefreshAsync()"/> or effectively equivalent methods.
        /// </summary>
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

        // postEditing: Is the entity param from the response of wbeditentity API call?
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
            Type = WbEntityType.Unknown;
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
                switch ((string)entity["type"])
                {
                    case "item":
                        Type = WbEntityType.Item;
                        break;
                    case "property":
                        Type = WbEntityType.Property;
                        break;
                    default:
                        Logger.LogWarning("Unrecognized entity type: {Type} for {Entity} on {Site}.",
                            (string)entity["type"], this, Site);
                        break;
                }
                var dataType = (string)entity["datatype"];
                if (dataType != null)
                    DataType = WbPropertyTypes.Get(dataType) ?? MissingPropertyType.Get(dataType, dataType);
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
                        SiteLinks = new WbEntitySiteLinkCollection(
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
                        Claims = new WbClaimCollection(jclaims.Values()
                            .SelectMany(jarray => jarray.Select(WbClaim.FromJson)));
                    }
                }
            }
            QueryOptions = options;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var en = Labels["en"];
            if (en != null) return en + "(" + Id + ")";
            return Id;
        }

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => Logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
        }
    }

    /// <summary>
    /// Wikibase entity types.
    /// </summary>
    public enum WbEntityType
    {
        /// <summary>
        /// Unknown entity type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A Wikibase item, usually having the prefix Q.
        /// </summary>
        Item = 0,

        /// <summary>
        /// A Wikibase property, usually having the prefix P.
        /// </summary>
        Property = 1,
    }

    [Flags]
    public enum EntityQueryOptions
    {

        /// <summary>No options.</summary>
        None = 0,

        /// <summary>Fetch page and last revision information.</summary>
        FetchInfo = 1,

        /// <summary>Fetch multilingual labels of the entity.</summary>
        FetchLabels = 2,

        /// <summary>Fetch multilingual aliases of the entity.</summary>
        FetchAliases = 4,

        /// <summary>Fetch multilingual descriptions of the entity.</summary>
        FetchDescriptions = 8,

        /// <summary>Fetch associated wiki site links.</summary> 
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

    public sealed class WbEntitySiteLinkCollection : UnorderedKeyedCollection<string, WbEntitySiteLink>
    {
        public WbEntitySiteLinkCollection()
        {

        }

        public WbEntitySiteLinkCollection(IEnumerable<WbEntitySiteLink> items)
        {
            Debug.Assert(items != null);
            foreach (var i in items) Add(i);
        }

        /// <inheritdoc />
        protected override string GetKeyForItem(WbEntitySiteLink item)
        {
            return item.Site;
        }
    }

    public sealed class WbClaimCollection : UnorderedKeyedMultiCollection<string, WbClaim>
    {

        public WbClaimCollection()
        {
            
        }

        public WbClaimCollection(IEnumerable<WbClaim> items)
        {
            Debug.Assert(items != null);
            foreach (var i in items) Add(i);
        }

        /// <inheritdoc />
        protected override string GetKeyForItem(WbClaim item)
        {
            return item.MainSnak.PropertyId;
        }
    }

}
