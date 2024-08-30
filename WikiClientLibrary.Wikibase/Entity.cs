using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikibase.DataTypes;
using WikiClientLibrary.Wikibase.Infrastructures;

namespace WikiClientLibrary.Wikibase;

/// <summary>
/// Provides information on a Wikibase item or property.
/// </summary>
/// <remarks>
/// The object represents a read-only snapshot of the Wikibase entity.
/// To edit the entity, use <see cref="EditAsync(IEnumerable{EntityEditEntry},string)"/> method.
/// </remarks>
public sealed partial class Entity : IEntity
{

    internal static readonly WbMonolingualTextCollection emptyStringDict
        = new WbMonolingualTextCollection { IsReadOnly = true };

    internal static readonly WbMonolingualTextsCollection emptyStringsDict
        = new WbMonolingualTextsCollection { IsReadOnly = true };

    internal static readonly EntitySiteLinkCollection emptySiteLinks
        = new EntitySiteLinkCollection { IsReadOnly = true };

    internal static readonly ClaimCollection emptyClaims
        = new ClaimCollection { IsReadOnly = true };

#region Static Methods

    /// <summary>
    /// Asynchronously gets the entity IDs with specified sequence of titles on the specified site.
    /// </summary>
    /// <param name="site">the Wikibase repository site.</param>
    /// <param name="siteName">the site name of the sitelinks.</param>
    /// <param name="titles">the article titles on the site <paramref name="siteName"/> to check for corresponding entity IDs.</param>
    /// <param name="cancellationToken">a token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/>, <paramref name="siteName"/>, or <paramref name="titles"/> is <c>null</c>.</exception>
    /// <returns>
    /// A asynchronous sequence of entity IDs (e.g. <c>Q1234</c>) in the identical order with <paramref name="titles"/>.
    /// If one or more entities are missing, the corresponding entity ID will be <c>null</c>.
    /// </returns>
    public static IAsyncEnumerable<string?> IdsFromSiteLinksAsync(WikiSite site, string siteName, IEnumerable<string> titles,
        CancellationToken cancellationToken = default)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (siteName == null) throw new ArgumentNullException(nameof(siteName));
        if (titles == null) throw new ArgumentNullException(nameof(titles));
        return WikibaseRequestHelper.EntityIdsFromSiteLinksAsync(site, siteName, titles, cancellationToken);
    }

    /// <summary>
    /// Asynchronously gets the entity IDs with specified sequence of titles on the specified site.
    /// </summary>
    /// <param name="site">The Wikibase repository site.</param>
    /// <param name="siteName">The site name of the sitelink.</param>
    /// <param name="title">The article title on the site <paramref name="siteName"/> to check for corresponding entity ID.</param>
    /// <param name="cancellationToken">a token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/>, <paramref name="siteName"/>, or <paramref name="title"/> is <c>null</c>.</exception>
    /// <returns>corresponding entity ID (e.g. <c>Q1234</c>), or <c>null</c> if no such entity can be found.</returns>
    public static async Task<string?> IdFromSiteLinkAsync(WikiSite site, string siteName, string title,
        CancellationToken cancellationToken = default)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (siteName == null) throw new ArgumentNullException(nameof(siteName));
        if (title == null) throw new ArgumentNullException(nameof(title));
        var result = await WikibaseRequestHelper.EntityIdsFromSiteLinksAsync(site, siteName, new[] { title }, cancellationToken)
            .FirstAsync(cancellationToken);
        return result;
    }

#endregion

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
    public string? Id { get; private set; }

    /// <summary>
    /// ID of the entity page.
    /// </summary>
    /// <remarks>
    /// The property value is invalidated after you have performed edits on this instance.
    /// To fetch the latest value, use <see cref="RefreshAsync(EntityQueryOptions)"/>.
    /// </remarks>
    public long PageId { get; private set; }

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
    public string? Title { get; private set; }

    /// <inheritdoc />
    public EntityType Type { get; private set; }

    /// <summary>
    /// For property entity, gets the data type of the property.
    /// </summary>
    public WikibaseDataType? DataType { get; private set; }

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
    public long LastRevisionId { get; private set; }

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
    public Task RefreshAsync(EntityQueryOptions options, ICollection<string>? languages)
    {
        return RefreshAsync(options, languages, CancellationToken.None);
    }

    /// <summary>
    /// Refreshes the entity information from Wikibase site.
    /// </summary>
    /// <param name="options">The options, including choosing the fields to fetch</param>
    /// <param name="languages">
    /// Filter down the internationalized values to the specified one or more language codes.
    /// Set to <c>null</c> to fetch for all available languages.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <seealso cref="EntityExtensions.RefreshAsync(IEnumerable{Entity},EntityQueryOptions,ICollection{string},CancellationToken)"/>
    public Task RefreshAsync(EntityQueryOptions options, ICollection<string>? languages, CancellationToken cancellationToken)
    {
        return WikibaseRequestHelper.RefreshEntitiesAsync(new[] { this }, options, languages, cancellationToken);
    }

    private static readonly IDictionary<string, JsonElement> emptyExtensionData = new Dictionary<string, JsonElement>();

    internal void LoadFromJson(JsonNode jentity, EntityQueryOptions options, bool isPostEditing)
    {
        var contract = jentity.Deserialize<Contracts.Entity>(MediaWikiHelper.WikiJsonSerializerOptions)!;
        LoadFromContract(contract, options, isPostEditing);
    }

    // postEditing: Is the entity param from the response of wbeditentity API call?
    internal void LoadFromContract(Contracts.Entity entity, EntityQueryOptions options, bool isPostEditing)
    {
        var extensionData = entity.ExtensionData ?? emptyExtensionData;
        var id = entity.Id;
        Debug.Assert(id != null);
        if ((options & EntityQueryOptions.SuppressRedirects) != EntityQueryOptions.SuppressRedirects
            && Id != null && Id != id)
        {
            // The page has been overwritten, or deleted.
            //logger.LogWarning("Detected change of page id for [[{Title}]]: {Id1} -> {Id2}.", Title, Id, id);
        }
        var serializable = extensionData.ContainsKey("missing")
            ? null
            : SerializableEntity.Load(entity);
        Id = id;
        Exists = serializable != null;
        Type = EntityType.Unknown;
        PageId = -1;
        NamespaceId = -1;
        Title = null;
        LastModified = DateTime.MinValue;
        LastRevisionId = 0;
        QueryOptions = options;
        if (serializable == null) return;
        serializable = SerializableEntity.Load(entity);
        Type = serializable.Type;
        DataType = serializable.DataType;

        if ((options & EntityQueryOptions.FetchInfo) == EntityQueryOptions.FetchInfo)
        {
            if (!isPostEditing)
            {
                // wbeditentity response does not have these properties.
                PageId = extensionData["pageid"].GetInt64();
                NamespaceId = extensionData["ns"].GetInt32();
                Title = extensionData["title"].GetString();
                LastModified = extensionData["modified"].GetDateTime();
            }
            LastRevisionId = extensionData["lastrevid"].GetInt64();
        }

        Labels = (options & EntityQueryOptions.FetchLabels) == EntityQueryOptions.FetchLabels && serializable.Labels.Count > 0
            ? serializable.Labels
            : emptyStringDict;
        Labels.IsReadOnly = true;

        Aliases = (options & EntityQueryOptions.FetchAliases) == EntityQueryOptions.FetchAliases && serializable.Aliases.Count > 0
            ? serializable.Aliases
            : emptyStringsDict;
        Aliases.IsReadOnly = true;

        Descriptions = (options & EntityQueryOptions.FetchDescriptions) == EntityQueryOptions.FetchDescriptions &&
                       serializable.Descriptions.Count > 0
            ? serializable.Descriptions
            : emptyStringDict;
        Descriptions.IsReadOnly = true;

        SiteLinks = (options & EntityQueryOptions.FetchSiteLinks) == EntityQueryOptions.FetchSiteLinks && serializable.SiteLinks.Count > 0
            ? serializable.SiteLinks
            : emptySiteLinks;
        SiteLinks.IsReadOnly = true;

        Claims = (options & EntityQueryOptions.FetchClaims) == EntityQueryOptions.FetchClaims && serializable.Claims.Count > 0
            ? serializable.Claims
            : emptyClaims;
        Claims.IsReadOnly = true;
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
    SuppressRedirects = 0x100,

}

/// <summary>
/// Represents a corresponding link for an entity to an external wiki site.
/// </summary>
public sealed class EntitySiteLink
{

    public EntitySiteLink(string site, string title) : this(site, title, null, null)
    {
    }

    public EntitySiteLink(string site, string title, IEnumerable<string>? badges) : this(site, title, badges, null)
    {
    }

    [JsonConstructor]
    public EntitySiteLink(string site, string title, IEnumerable<string>? badges, string? url)
    {
        Site = site;
        Title = title;
        Badges = badges switch
        {
            null => ImmutableList<string>.Empty,
            IImmutableList<string> imBadges => imBadges,
            var b => b.ToImmutableList(),
        };
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
    public string? Url { get; }

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
