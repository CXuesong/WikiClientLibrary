using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikiClientLibrary.Wikibase;

/// <summary>
/// Represents an entity that can be loaded from or serialized into JSON.
/// </summary>
/// <remarks>
/// When working with JSON dump of Wikibase items, this class may provide some facility.
/// </remarks>
public class SerializableEntity : IEntity
{

    private WbMonolingualTextCollection _Labels;
    private WbMonolingualTextCollection _Descriptions;
    private WbMonolingualTextsCollection _Aliases;
    private EntitySiteLinkCollection _SiteLinks;
    private ClaimCollection _Claims;
    private EntityType _Type;

    public SerializableEntity() : this(true)
    {
    }

    private SerializableEntity(bool initialized)
    {
        if (initialized)
        {
            _Labels = new WbMonolingualTextCollection();
            _Descriptions = new WbMonolingualTextCollection();
            _Aliases = new WbMonolingualTextsCollection();
            _SiteLinks = new EntitySiteLinkCollection();
            _Claims = new ClaimCollection();
        }
        else
        {
            // Initialize with readonly placeholders. Caller needs to replace the whole property value afterwards.
            _Labels = Entity.emptyStringDict;
            _Descriptions = Entity.emptyStringDict;
            _Aliases = Entity.emptyStringsDict;
            _SiteLinks = Entity.emptySiteLinks;
            _Claims = Entity.emptyClaims;
        }
    }

    /// <inheritdoc />
    public string? Id { get; set; }

    /// <inheritdoc />
    public WikibaseDataType? DataType { get; set; }

    /// <inheritdoc />
    public WbMonolingualTextCollection Labels
    {
        get { return _Labels; }
        set { _Labels = value ?? new WbMonolingualTextCollection(); }
    }

    /// <inheritdoc />
    public WbMonolingualTextCollection Descriptions
    {
        get { return _Descriptions; }
        set { _Descriptions = value ?? new WbMonolingualTextCollection(); }
    }

    /// <inheritdoc />
    public WbMonolingualTextsCollection Aliases
    {
        get { return _Aliases; }
        set { _Aliases = value ?? new WbMonolingualTextsCollection(); }
    }

    /// <inheritdoc />
    public EntitySiteLinkCollection SiteLinks
    {
        get { return _SiteLinks; }
        set { _SiteLinks = value ?? new EntitySiteLinkCollection(); }
    }

    /// <inheritdoc />
    public ClaimCollection Claims
    {
        get { return _Claims; }
        set { _Claims = value ?? new ClaimCollection(); }
    }

    /// <inheritdoc />
    public EntityType Type
    {
        get { return _Type; }
        set
        {
            CheckEntityType(value);
            _Type = value;
        }
    }

    private void CheckEntityType(EntityType value)
    {
        if (value != EntityType.Item && value != EntityType.Property && value != EntityType.Unknown)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    internal static EntityType ParseEntityType(string? value)
    {
        return value switch
        {
            "item" => EntityType.Item,
            "property" => EntityType.Property,
            _ => EntityType.Unknown
        };
    }

    private static string ToString(EntityType value)
    {
        return value switch
        {
            EntityType.Item => "item",
            EntityType.Property => "property",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Creates a new instance of <see cref="SerializableEntity"/> from an existing
    /// <see cref="IEntity"/> instance.
    /// </summary>
    /// <param name="entity">The existing entity. A shallow copy of values will be made from it.</param>
    public static SerializableEntity Load(IEntity entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var inst = new SerializableEntity(false)
        {
            Type = entity.Type,
            DataType = entity.DataType,
            Id = entity.Id,
            Aliases = new WbMonolingualTextsCollection(entity.Aliases),
            Descriptions = new WbMonolingualTextCollection(entity.Descriptions),
            Labels = new WbMonolingualTextCollection(entity.Labels),
            Claims = new ClaimCollection(entity.Claims),
            SiteLinks = new EntitySiteLinkCollection(entity.SiteLinks)
        };
        return inst;
    }

    internal static SerializableEntity Load(Contracts.Entity entity)
    {
        // If caller deserializes entity from EOF-ed stream, the caller can get null.
        Debug.Assert(entity != null);
        var inst = new SerializableEntity(false) { Id = entity.Id, Type = ParseEntityType(entity.Type) };
        if (!string.IsNullOrEmpty(entity.DataType))
            inst.DataType = BuiltInDataTypes.Get(entity.DataType) ?? MissingPropertyType.Get(entity.DataType, entity.DataType);
        inst.Labels = new WbMonolingualTextCollection(
            entity.Labels?.Values.Select(v => new WbMonolingualText(v.Language, v.Value))
            ?? Enumerable.Empty<WbMonolingualText>());
        inst.Descriptions = new WbMonolingualTextCollection(
            entity.Descriptions?.Values.Select(v => new WbMonolingualText(v.Language, v.Value))
            ?? Enumerable.Empty<WbMonolingualText>());
        inst.Aliases = new WbMonolingualTextsCollection(
            entity.Aliases?.Values.SelectMany(vs => vs).Select(v => new WbMonolingualText(v.Language, v.Value))
            ?? Enumerable.Empty<WbMonolingualText>());
        inst.SiteLinks = new EntitySiteLinkCollection(
            entity.Sitelinks?.Values.Select(v => new EntitySiteLink(v.Site, v.Title, v.Badges))
            ?? Enumerable.Empty<EntitySiteLink>());
        if (entity.Claims == null || entity.Claims.Count == 0)
        {
            inst.Claims = new ClaimCollection();
        }
        else
        {
            // { claims : { P47 : [ {}, {}, ... ], P105 : ... } }
            inst.Claims = new ClaimCollection(entity.Claims.Values.SelectMany(c => c).Select(Claim.FromContract));
        }
        return inst;
    }

    /// <summary>
    /// Creates a new instance of <see cref="SerializableEntity"/> from <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="entity">The serialized entity JSON.</param>
    public static SerializableEntity Load(JsonObject entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var contract = entity.Deserialize<Contracts.Entity>(MediaWikiHelper.WikiJsonSerializerOptions)!;
        return Load(contract);
    }

    /// <summary>
    /// Loads exactly 1 <see cref="SerializableEntity"/> from the <see cref="Stream"/>.
    /// </summary>
    /// <param name="s">The stream from which to read serialized entity JSON.</param>
    /// <returns>The deserialized entity, or <c>null</c> if the JSON token contained in the stream input is <c>null</c>.</returns>
    /// <exception cref="JsonException"><param name="s" /> does not contain a valid JSON object; OR there is extraneous content after the JSON object.</exception>
    public static SerializableEntity? Load(Stream s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var contract = JsonSerializer.Deserialize<Contracts.Entity>(s, MediaWikiHelper.WikiJsonSerializerOptions);
        return contract == null ? null : Load(contract);
    }

    /// <summary>
    /// Enumerates all the entities from JSON array of serialized entities contained in the string.
    /// </summary>
    /// <param name="entitiesJson">The serialized entities JSON string.</param>
    /// <remarks></remarks>
    public static IEnumerable<SerializableEntity?> ParseAll(string entitiesJson)
    {
        if (entitiesJson == null) throw new ArgumentNullException(nameof(entitiesJson));
        var deserialized = JsonSerializer.Deserialize<List<Contracts.Entity?>>(entitiesJson, MediaWikiHelper.WikiJsonSerializerOptions);
        if (deserialized == null) return [];
        return deserialized.Select(c => c == null ? null : Load(c));
    }

    /// <summary>
    /// Asynchronously enumerates a JSON array of the serialized Wikibase entities contained in the specified stream.
    /// </summary>
    /// <param name="s">The stream from which to read serialized entity JSON.</param>
    /// <exception cref="JsonException">The stream does not contain a JSON array of objects; OR there is extraneous content after the root JSON array.</exception>
    /// <returns>
    /// A sequence that, when enumerated asynchronously, will read a JSON array from the stream, and parses it
    /// into a sequence of <see cref="SerializableEntity"/>s.
    /// If there is <c>null</c> in the JSON array, <c>null</c> will be enumerated in the sequence.
    /// </returns>
    /// <remarks>
    /// <para>This method is recommended when you are working with a large JSON dump of Wikibase entities,
    /// because it only put the current enumerated entity in the memory. Still, you can use
    /// <see cref="AsyncEnumerable.ToListAsync{TSource}"/> to materialize all the entities at once.</para>
    /// <para>Due to the buffering mechanism of the implementation, if you stopped enumeration at the middle
    /// of the returned sequence, <paramref name="s"/> may stop at a point later than the last deserialized
    /// JSON array item (JSON object).</para>
    /// <para>Unlike other <see cref="overloads:Load"/> overloads, this method returns empty sequence
    /// if <paramref name="s"/> does not contain any content.</para>
    /// </remarks>
    public static IAsyncEnumerable<SerializableEntity?> LoadAllAsync(Stream s)
    {
        return JsonSerializer
            .DeserializeAsyncEnumerable<Contracts.Entity>(s, MediaWikiHelper.WikiJsonSerializerOptions)
            .Select(contract => contract == null ? null : Load(contract));
    }

    /// <inheritdoc cref="LoadAllAsync"/>
    /// <summary>
    /// Synchronously enumerates a JSON array of the serialized Wikibase entities contained in the specified stream.
    /// </summary>
    /// <returns>
    /// A sequence that, when enumerated, will read a JSON array from the stream, and parses it
    /// into a sequence of <see cref="SerializableEntity"/>s.
    /// If there is <c>null</c> in the JSON array, <c>null</c> will be enumerated in the sequence.
    /// </returns>
    public static IEnumerable<SerializableEntity?> LoadAll(Stream s)
        => LoadAllAsync(s).ToEnumerable();

    /// <inheritdoc cref="Load(Stream)"/>
    /// <summary>
    /// Loads exactly 1 <see cref="SerializableEntity"/> from the specified file.
    /// </summary>
    /// <param name="fileName">The path of file containing the serialized JSON of a single entity.</param>
    /// <seealso cref="LoadAll(string)"/>
    public static SerializableEntity? Load(string fileName)
    {
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        using var s = File.OpenRead(fileName);
        return Load(s);
    }

    /// <inheritdoc cref="LoadAll(Stream)"/>
    /// <summary>
    /// Synchronously enumerates a JSON array of the serialized Wikibase entities contained in the specified stream.
    /// </summary>
    /// <param name="fileName">The path of file containing the serialized JSON array of entities.</param>
    /// <remarks></remarks>
    /// <seealso cref="Load(string)"/>
    public static IEnumerable<SerializableEntity?> LoadAll(string fileName)
    {
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        using var reader = File.OpenRead(fileName);
        // Ensure reader is disposed after we have finished / aborted enumeration.
        foreach (var i in LoadAll(reader))
            yield return i;
    }

    /// <inheritdoc cref="Load(Stream)"/>
    /// <summary>
    /// Creates a new instance of <see cref="SerializableEntity"/> from JSON string.
    /// </summary>
    /// <param name="entityJson">The serialized entity JSON string.</param>
    public static SerializableEntity? Parse(string entityJson)
    {
        if (entityJson == null) throw new ArgumentNullException(nameof(entityJson));
        var contract = JsonSerializer.Deserialize<Contracts.Entity>(entityJson, MediaWikiHelper.WikiJsonSerializerOptions);
        return contract == null ? null : Load(contract);
    }

    private static IDictionary<string, Contracts.MonolingualText> ToContract(WbMonolingualTextCollection value)
    {
        return value.ToDictionary(p => p.Language, p => new Contracts.MonolingualText { Language = p.Language, Value = p.Text });
    }

    private static IDictionary<string, ICollection<Contracts.MonolingualText>> ToContract(WbMonolingualTextsCollection value)
    {
        return value.Languages.ToDictionary(lang => lang,
            lang => (ICollection<Contracts.MonolingualText>)value[lang]
                .Select(t => new Contracts.MonolingualText { Language = lang, Value = t }).ToList());
    }

    private Contracts.Entity ToContract()
    {
        var obj = new Contracts.Entity
        {
            Id = Id,
            Type = ToString(Type),
            Labels = ToContract(_Labels),
            Aliases = ToContract(_Aliases),
            Descriptions = ToContract(_Descriptions),
            Sitelinks = SiteLinks.ToDictionary(link => link.Site,
                link => new Contracts.SiteLink { Site = link.Site, Title = link.Title, Badges = link.Badges.ToList() }),
            Claims = Claims.GroupBy(c => c.MainSnak.PropertyId).ToDictionary(g => g.Key,
                g => (ICollection<Contracts.Claim>)g.Select(c => c.ToContract(false)).ToList())
        };
        return obj;
    }

    /// <summary>
    /// Serializes the entity into JSON.
    /// </summary>
    public JsonObject ToJsonObject()
    {
        return JsonSerializer.SerializeToNode(ToContract(), MediaWikiHelper.WikiJsonSerializerOptions)!.AsObject();
    }

    /// <summary>
    /// Serializes the entity into compact JSON string.
    /// </summary>
    public string ToJsonString()
    {
        return JsonSerializer.Serialize(ToContract(), MediaWikiHelper.WikiJsonSerializerOptions);
    }

    /// <summary>
    /// Writes the serialized entity into <see cref="Stream"/>.
    /// </summary>
    public void WriteTo(Stream s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        JsonSerializer.Serialize(s, ToContract(), MediaWikiHelper.WikiJsonSerializerOptions);
    }

}
