using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Sites;
using System.Threading;
using WikiClientLibrary.Wikibase.DataTypes;
using WikiClientLibrary.Wikibase.Infrastructures;

namespace WikiClientLibrary.Wikibase
{

    /// <summary>
    /// Provides information on a Wikibase item or property.
    /// </summary>
    /// <remarks>
    /// The object represents a readonly snapshot of the Wikibase entity.
    /// To edit the entity, use <see cref="EditAsync(IEnumerable{EntityEditEntry},string)"/> method.
    /// </remarks>
    public sealed partial class Entity : IEntity
    {
        internal static readonly WbMonolingualTextCollection emptyStringDict
            = new WbMonolingualTextCollection {IsReadOnly = true};

        internal static readonly WbMonolingualTextsCollection emptyStringsDict
            = new WbMonolingualTextsCollection {IsReadOnly = true};

        internal static readonly EntitySiteLinkCollection emptySiteLinks
            = new EntitySiteLinkCollection {IsReadOnly = true};

        internal static readonly ClaimCollection emptyClaims
            = new ClaimCollection {IsReadOnly = true};
        
        /// <summary>
        /// Initializes a new <see cref="Entity"/> entity from Wikibase site,
        /// marked for creation.
        /// </summary>
        /// <param name="site">Wikibase site.</param>
        /// <param name="type">Type of the new entity.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is neither
        /// <see cref="EntityType.Item"/> nor <see cref="EntityType.Property"/>.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> is <c>null</c>.</exception>
        public Entity(WikiSite site, EntityType type)
        {
            if (type != EntityType.Item && type != EntityType.Property)
                throw new ArgumentOutOfRangeException(nameof(type));
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Id = null;
            Type = type;
        }

        /// <summary>
        /// Initializes a new <see cref="Entity"/> entity from Wikibase site
        /// and existing entity ID.
        /// </summary>
        /// <param name="site">Wikibase site.</param>
        /// <param name="id">Entity or property ID, without <c>Property:</c> prefix.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="id"/> is <c>null</c>.</exception>
        public Entity(WikiSite site, string id)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public WikiSite Site { get; }
        
        /// <summary>
        /// Id of the entity.
        /// </summary>
        /// <value>Item or Property ID, OR <c>null</c> if this is a new entity that has not made any changes.</value>
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

        /// <inheritdoc />
        public EntityType Type { get; private set; }

        /// <summary>
        /// For property entity, gets the data type of the property.
        /// </summary>
        public WikibaseDataType DataType { get; private set; }

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

        /// <inheritdoc />
        public WbMonolingualTextCollection Labels { get; private set; } = emptyStringDict;

        /// <inheritdoc />
        public WbMonolingualTextCollection Descriptions { get; private set; } = emptyStringDict;

        /// <inheritdoc />
        public WbMonolingualTextsCollection Aliases { get; private set; } = emptyStringsDict;

        /// <inheritdoc />
        public EntitySiteLinkCollection SiteLinks { get; private set; } = emptySiteLinks;

        /// <inheritdoc />
        public ClaimCollection Claims { get; private set; } = emptyClaims;

        /// <summary>
        /// The last query options used with <see cref="RefreshAsync()"/> or effectively equivalent methods.
        /// </summary>
        public EntityQueryOptions QueryOptions { get; private set; }

        /// <inheritdoc cref="RefreshAsync(EntityQueryOptions,ICollection{string},CancellationToken)"/>
        /// <summary>
        /// Refreshes the basic entity information from Wikibase site.
        /// </summary>
        /// <remarks>This overload uses <see cref="EntityQueryOptions.FetchInfo"/> option to fetch basic information.</remarks>
        /// <seealso cref="EntityExtensions.RefreshAsync(IEnumerable{Entity})"/>
        public Task RefreshAsync()
        {
            return RefreshAsync(EntityQueryOptions.FetchInfo, null, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(EntityQueryOptions,ICollection{string},CancellationToken)"/>
        /// <seealso cref="EntityExtensions.RefreshAsync(IEnumerable{Entity},EntityQueryOptions)"/>
        public Task RefreshAsync(EntityQueryOptions options)
        {
            return RefreshAsync(options, null, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(EntityQueryOptions,ICollection{string},CancellationToken)"/>
        /// <seealso cref="EntityExtensions.RefreshAsync(IEnumerable{Entity},EntityQueryOptions,ICollection{string})"/>
        public Task RefreshAsync(EntityQueryOptions options, ICollection<string> languages)
        {
            return RefreshAsync(options, languages, CancellationToken.None);
        }

        /// <summary>
        /// Refreshes the entity information from Wikibase site.
        /// </summary>
        /// <param name="options">The options, including choosing the fields to fetch</param>
        /// <param name="languages">
        /// Filter down the internationalized values to the specified one or more language codes.
        /// Set to <c>null</c> for all available languages.
        /// </param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <seealso cref="EntityExtensions.RefreshAsync(IEnumerable{Entity},EntityQueryOptions,ICollection{string},CancellationToken)"/>
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
            Type = EntityType.Unknown;
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
                        Type = EntityType.Item;
                        break;
                    case "property":
                        Type = EntityType.Property;
                        break;
                    default:
                        Site.Logger.LogWarning("Unrecognized entity type: {Type} for {Entity} on {Site}.",
                            (string)entity["type"], this, Site);
                        break;
                }
                var dataType = (string)entity["datatype"];
                if (dataType != null)
                    DataType = BuiltInDataTypes.Get(dataType) ?? MissingPropertyType.Get(dataType, dataType);
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
                        SiteLinks = new EntitySiteLinkCollection(
                            jlinks.Values().Select(t => t.ToObject<EntitySiteLink>(Utility.WikiJsonSerializer)));
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
                        Claims = new ClaimCollection(jclaims.Values()
                                .SelectMany(jarray => jarray.Select(Claim.FromJson)))
                            {IsReadOnly = true};
                    }
                }
            }
            QueryOptions = options;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var en = Labels?["en"];
            var id = Id ?? ("<New " + Type + ">");
            if (en != null) return en + "(" + id + ")";
            return id;
        }

    }

    /// <summary>
    /// Wikibase entity types.
    /// </summary>
    public enum EntityType
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
        /// A Wikibase property, usually having the prefix P and in <c>Property</c> namespace.
        /// </summary>
        Property = 1,
    }

    /// <summary>
    /// Provides options for fetching entity information from server.
    /// </summary>
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

    /// <summary>
    /// Represents a corresponding link for an entity to an external wiki site.
    /// </summary>
    public sealed class EntitySiteLink
    {

        private static readonly IReadOnlyList<string> emptyBadges = new ReadOnlyCollection<string>(new string[0]);

        public EntitySiteLink(string site, string title) : this(site, title, null, null)
        {
        }

        public EntitySiteLink(string site, string title, IReadOnlyList<string> badges) : this(site, title, badges, null)
        {
        }

        [JsonConstructor]
        public EntitySiteLink(string site, string title, IReadOnlyList<string> badges, string url)
        {
            Site = site;
            Title = title;
            if (badges is IList<string> lb && !lb.IsReadOnly)
                Badges = new ReadOnlyCollection<string>(lb);
            else
                Badges = badges ?? emptyBadges;
            Url = url;
        }

        /// <summary>
        /// The site name.
        /// </summary>
        /// <remarks>For a complete list of site names available for Wikidata,
        /// see https://www.wikidata.org/w/api.php?action=help&amp;modules=wbgetentities .</remarks>
        public string Site { get; }

        /// <summary>
        /// The local title on the specified wiki site.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// The badges for the title on the specified wiki site.
        /// </summary>
        public IReadOnlyList<string> Badges { get; }

        /// <summary>
        /// The URL to the title on the specified wiki site.
        /// </summary>
        public string Url { get; }

    }

    public sealed class EntitySiteLinkCollection : UnorderedKeyedCollection<string, EntitySiteLink>
    {
        public EntitySiteLinkCollection()
        {

        }

        public EntitySiteLinkCollection(IEnumerable<EntitySiteLink> items)
        {
            Debug.Assert(items != null);
            foreach (var i in items) Add(i);
        }

        /// <inheritdoc />
        protected override string GetKeyForItem(EntitySiteLink item)
        {
            return item.Site;
        }
    }

    public sealed class ClaimCollection : UnorderedKeyedMultiCollection<string, Claim>
    {

        public ClaimCollection()
        {
            
        }

        public ClaimCollection(IEnumerable<Claim> items)
        {
            Debug.Assert(items != null);
            foreach (var i in items) Add(i);
        }

        /// <inheritdoc />
        protected override string GetKeyForItem(Claim item)
        {
            return item.MainSnak.PropertyId;
        }
    }

}
