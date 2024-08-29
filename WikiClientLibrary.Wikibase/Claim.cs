using System.Diagnostics;
using System.Text.Json.Nodes;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikiClientLibrary.Wikibase;

/// <summary>
/// Represents a claim applied to a Wikibase entity.
/// </summary>
[DebuggerDisplay("{MainSnak}; {Qualifiers.Count} qualifier(s),  {References.Count} reference(s)")]
public sealed class Claim
{

    private readonly List<Snak> _Qualifiers = new List<Snak>();
    private readonly List<ClaimReference> _References = new List<ClaimReference>();

    /// <summary>
    /// Initializes a new <see cref="Claim"/> instance with the specified main snak.
    /// </summary>
    /// <param name="mainSnak">The main snak containing the property and value of the claim.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mainSnak"/> is <c>null</c>.</exception>
    public Claim(Snak mainSnak)
    {
        MainSnak = mainSnak ?? throw new ArgumentNullException(nameof(mainSnak));
    }

    /// <summary>
    /// Initializes a new <see cref="Claim"/> instance with the main snak set to property id.
    /// </summary>
    /// <param name="mainSnakPropertyId">The property ID of the auto-constructed main snak.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mainSnakPropertyId"/> is <c>null</c>.</exception>
    public Claim(string mainSnakPropertyId)
    {
        if (mainSnakPropertyId == null) throw new ArgumentNullException(nameof(mainSnakPropertyId));
        MainSnak = new Snak(mainSnakPropertyId);
    }

    /// <summary>
    /// Initializes a new <see cref="Claim"/> instance with the main snak set to property id and value.
    /// </summary>
    /// <param name="propertyId">The property ID of the auto-constructed main snak.</param>
    /// <param name="dataValue">The data value of the main snak.</param>
    /// <param name="dataType">The data type of the main snak.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyId"/> or <paramref name="dataType"/> is <c>null</c>.</exception>
    public Claim(string propertyId, object dataValue, WikibaseDataType dataType)
    {
        MainSnak = new Snak(propertyId, dataValue, dataType);
    }

    /// <summary>
    /// Gets the main snak of the claim.
    /// </summary>
    /// <remarks>This property is read-only; however you can change its content exception <see cref="Snak.PropertyId"/> value.</remarks>
    public Snak MainSnak { get; }

    /// <summary>Claim ID.</summary>
    /// <value>Claim GUID, or <c>null</c> for a newly-created claim yet to be submitted.</value>
    public string? Id { get; set; }

    /// <summary>The type of the claim.</summary>
    /// <remarks>The value often is <c>statement</c>.</remarks>
    public string Type { get; set; } = "statement";

    /// <summary>Rank of the claim.</summary>
    /// <remarks>The value often is <c>normal</c>.</remarks>
    public string Rank { get; set; } = "normal";

    /// <summary>
    /// Gets/sets the collection of qualifiers.
    /// </summary>
    public IList<Snak> Qualifiers => _Qualifiers;

    /// <summary>
    /// Gets/sets the collection of citations for the claim.
    /// </summary>
    public IList<ClaimReference> References => _References;

    /// <inheritdoc />
    public override string ToString()
    {
        return MainSnak.ToString();
    }

    internal static IEnumerable<TValue> EnumWithOrder<TValue>(IDictionary<string, ICollection<TValue>> dict, IList<string>? order)
    {
        // Before #84516 Wikibase did not implement snaks-order.
        // https://gerrit.wikimedia.org/r/#/c/84516/
        // Note: after a while wmf decided to obsolete statement order,
        // while to keep the qualifier order functionality as-is.
        // https://phabricator.wikimedia.org/T99243
        // https://lists.wikimedia.org/pipermail/wikidata-tech/2017-November/001207.html
        Debug.Assert(dict != null);
        if (order == null)
            return dict.Values.SelectMany(c => c).ToList();
        try
        {
            if (order.Count == dict.Count)
                return order.Select(key => dict[key]).SelectMany(c => c);
        }
        catch (KeyNotFoundException)
        {
        }
        // Ill-formed dict-order arguments
        throw new ArgumentException("The ordered list and keys in the dictionary does not correspond.");
    }

    internal static Dictionary<string, ICollection<TValue>> GroupIntoDictionary<TValue>(IEnumerable<TValue> items,
        Func<TValue, string> keySelector)
    {
        var dict = new Dictionary<string, ICollection<TValue>>();
        foreach (var i in items)
        {
            var key = keySelector(i);
            if (!dict.TryGetValue(key, out var g))
            {
                g = new List<TValue>();
                dict.Add(key, g);
            }
            g.Add(i);
        }
        return dict;
    }

    internal static Claim FromContract(Contracts.Claim claim)
    {
        Debug.Assert(claim != null);
        if (claim.MainSnak == null) throw new ArgumentException("Invalid claim. MainSnak is null.", nameof(claim));
        var inst = new Claim(Snak.FromContract(claim.MainSnak));
        inst.LoadFromContract(claim);
        return inst;
    }

    private void LoadFromContract(Contracts.Claim claim)
    {
        Debug.Assert(claim != null);
        Id = claim.Id;
        Type = claim.Type ?? "";
        Rank = claim.Rank ?? "";
        _Qualifiers.Clear();
        if (claim.Qualifiers != null)
            _Qualifiers.AddRange(EnumWithOrder(claim.Qualifiers, claim.QualifiersOrder).Select(Snak.FromContract));
        _References.Clear();
        if (claim.References != null)
            _References.AddRange(claim.References.Select(ClaimReference.FromContract));
    }

    internal Contracts.Claim ToContract(bool identifierOnly)
    {
        var obj = new Contracts.Claim { Id = Id, Type = Type, Rank = Rank };
        if (identifierOnly) return obj;
        obj.MainSnak = MainSnak.ToContract();
        obj.Qualifiers = Qualifiers.Select(q => q.ToContract())
            .GroupBy(q => q.Property).ToDictionary(g => g.Key, g => (ICollection<Contracts.Snak>)g.ToList());
        obj.References = References.Select(r => r.ToContract()).ToList();
        return obj;
    }

}

/// <summary>
/// Represents the a reference (citation) item of a <see cref="Claim"/>.
/// </summary>
public sealed class ClaimReference
{

    private readonly List<Snak> _Snaks = new List<Snak>();

    public ClaimReference()
    {
    }

    public ClaimReference(IEnumerable<Snak> snaks)
    {
        if (snaks == null) throw new ArgumentNullException(nameof(snaks));
        _Snaks.AddRange(snaks);
    }

    public ClaimReference(params Snak[] snaks)
    {
        _Snaks.AddRange(snaks);
    }

    public IList<Snak> Snaks => _Snaks;

    public string Hash { get; set; } = "";

    internal static ClaimReference FromContract(Contracts.Reference reference)
    {
        Debug.Assert(reference != null);
        var inst = new ClaimReference();
        inst.LoadFromContract(reference);
        return inst;
    }

    internal void LoadFromContract(Contracts.Reference reference)
    {
        Debug.Assert(reference != null);
        _Snaks.Clear();
        if (reference.Snaks != null)
        {
            _Snaks.AddRange(Claim.EnumWithOrder(reference.Snaks, reference.SnaksOrder).Select(Snak.FromContract));
        }
        Hash = reference.Hash ?? "";
    }

    internal Contracts.Reference ToContract()
    {
        return new Contracts.Reference
        {
            Hash = Hash,
            Snaks = Claim.GroupIntoDictionary(Snaks.Select(s => s.ToContract()), s => s.Property),
            SnaksOrder = Snaks.Select(s => s.PropertyId).Distinct().ToList()
        };
    }

}

/// <summary>
/// Represents a "snak", i.e. a pair of property and value.
/// </summary>
/// <remarks>
/// To compare the equality of two snaks' values, consider using
/// <see cref="JToken.DeepEquals(JToken,JToken)"/> on <see cref="RawDataValue"/>.
/// </remarks>
public sealed class Snak
{

    private static readonly JsonObject DirtyDataValuePlaceholder = new();

    private object? _DataValue;
    private JsonObject? _RawDataValue;

    /// <summary>
    /// Initializes a snak with specified property ID and empty value.
    /// </summary>
    /// <param name="propertyId">The property id.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyId"/> is <c>null</c>.</exception>
    public Snak(string propertyId)
    {
        PropertyId = propertyId ?? throw new ArgumentNullException(nameof(propertyId));
    }

    /// <summary>
    /// Initializes a snak with specified property ID and data value.
    /// </summary>
    /// <param name="propertyId">The property id.</param>
    /// <param name="dataValue">The data value of the property.</param>
    /// <param name="dataType">The data value type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyId"/> or <paramref name="dataType"/> is <c>null</c>.</exception>
    public Snak(string propertyId, object dataValue, WikibaseDataType dataType)
    {
        PropertyId = propertyId ?? throw new ArgumentNullException(nameof(propertyId));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        DataValue = dataValue;
    }

    /// <summary>
    /// Initializes a snak with specified property ID and raw data value.
    /// </summary>
    /// <param name="propertyId">The property id.</param>
    /// <param name="rawDataValue">The raw JSON data value of the property.</param>
    /// <param name="dataType">The data value type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyId"/> or <paramref name="dataType"/> is <c>null</c>.</exception>
    public Snak(string propertyId, JsonObject rawDataValue, WikibaseDataType dataType)
    {
        PropertyId = propertyId ?? throw new ArgumentNullException(nameof(propertyId));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        RawDataValue = rawDataValue;
    }

    /// <summary>
    /// Initializes a snak with specified property ID and snak type.
    /// </summary>
    /// <param name="propertyId">The property id.</param>
    /// <param name="snakType">Snak type.</param>
    /// <remarks>
    /// If you set <paramref name="snakType"/> to <see cref="SnakType.Value"/>, remember to set
    /// <see cref="DataType"/> and <see cref="DataValue"/> to valid values afterwards.
    /// </remarks>
    public Snak(string propertyId, SnakType snakType)
    {
        PropertyId = propertyId ?? throw new ArgumentNullException(nameof(propertyId));
        SnakType = snakType;
    }

    /// <summary>
    /// Initializes a snak with specified property and data value.
    /// </summary>
    /// <param name="property">The property. It should have valid <see cref="Entity.Id"/> and <see cref="Entity.DataType"/>.</param>
    /// <param name="dataValue">The data value of the property.</param>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="property"/> is not Wikibase property, or does not contain required information.</exception>
    public Snak(Entity property, object dataValue)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (property.Type != EntityType.Property)
            throw new ArgumentException("The entity is not Wikibase property.", nameof(property));
        if (property.Id == null)
            throw new ArgumentException("The entity does not contain ID information.", nameof(property));
        if (property.DataType == null)
            throw new ArgumentException("The entity does not contain data type information.", nameof(property));
        PropertyId = property.Id;
        DataType = property.DataType;
        DataValue = dataValue;
    }

    /// <summary>Snak type.</summary>
    public SnakType SnakType { get; set; }

    /// <summary>Property ID, with "P" prefix.</summary>
    public string PropertyId { get; }

    /// <summary>Snak hash.</summary>
    /// <remarks>Main snak does not have hash; thus this property can be null.</remarks>
    public string? Hash { get; set; }

    /// <summary>Raw JSON value of <c>datavalue</c> node.</summary>
    /// <remarks>For the cases when <see cref="SnakType"/> is not <see cref="SnakType.Value"/>, this property should be <c>null</c>.</remarks>
    public JsonObject? RawDataValue
    {
        get
        {
            if (_RawDataValue != DirtyDataValuePlaceholder)
                return _RawDataValue;
            if (_DataValue == null)
            {
                _RawDataValue = null;
                return null;
            }
            Debug.Assert(_DataValue != DirtyDataValuePlaceholder);
            if (DataType == null) throw new InvalidOperationException("DataType is null.");
            var data = new JsonObject { { "value", DataType.ToJson(_DataValue) }, { "type", DataType.ValueTypeName } };
            _RawDataValue = data;
            return data;
        }
        set
        {
            _RawDataValue = value;
            _DataValue = DirtyDataValuePlaceholder;
        }
    }

    /// <summary>Parsed value of <c>datavalue</c> node.</summary>
    /// <remarks>For the cases when <see cref="SnakType"/> is not <see cref="Wikibase.SnakType.Value"/>, this property should be <c>null</c>.</remarks>
    public object? DataValue
    {
        get
        {
            if (_DataValue != DirtyDataValuePlaceholder) return _DataValue;
            var raw = _RawDataValue;
            if (raw == null)
            {
                _DataValue = null;
                return null;
            }
            Debug.Assert(raw != DirtyDataValuePlaceholder);
            if (DataType == null) throw new InvalidOperationException("DataType is null.");
            var valueType = (string?)raw["type"];
            if (valueType != null && valueType != DataType.ValueTypeName)
                throw new NotSupportedException($"Parsing value type \"{valueType}\" is not supported by {DataType}.");
            var value = DataType.Parse(raw["value"]);
            _DataValue = value;
            return value;
        }
        set
        {
            _DataValue = value;
            _RawDataValue = DirtyDataValuePlaceholder;
        }
    }

    /// <summary>Data value type.</summary>
    public WikibaseDataType? DataType { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        // TODO need something link TryGetDataValue to handle unknown data types
        var valueExpr = SnakType switch
        {
            SnakType.Value => DataValue?.ToString(),
            SnakType.SomeValue => "[SomeValue]",
            SnakType.NoValue => "[NoValue]",
            _ => "[Invalid SnakType]"
        };
        return $"{PropertyId} = {valueExpr}";
    }

    internal static SnakType ParseSnakType(string expr)
    {
        if (expr == null) throw new ArgumentNullException(nameof(expr));
        return expr switch
        {
            "value" => SnakType.Value,
            "somevalue" => SnakType.SomeValue,
            "novalue" => SnakType.NoValue,
            _ => throw new ArgumentException("Invalid SnackType expression.", nameof(expr))
        };
    }

    internal static string ParseSnakType(SnakType value)
    {
        return value switch
        {
            SnakType.Value => "value",
            SnakType.SomeValue => "somevalue",
            SnakType.NoValue => "novalue",
            _ => throw new ArgumentException("Invalid SnackType value.", nameof(value))
        };
    }

    internal static Snak FromContract(Contracts.Snak snak)
    {
        Debug.Assert(snak != null);
        var inst = new Snak(snak.Property);
        inst.LoadFromContract(snak);
        return inst;
    }

    private void LoadFromContract(Contracts.Snak snak)
    {
        Debug.Assert(snak != null);
        SnakType = ParseSnakType(snak.SnakType!);
        Hash = snak.Hash ?? "";
        RawDataValue = snak.DataValue;
        DataType = string.IsNullOrEmpty(snak.DataType)
            ? null
            : BuiltInDataTypes.Get(snak.DataType) ?? MissingPropertyType.Get(snak.DataType, (string)snak.DataValue?["type"]);
    }

    internal Contracts.Snak ToContract()
    {
        if (DataType == null)
            throw new InvalidOperationException("DataType is required on serialization.");
        return new Contracts.Snak
        {
            SnakType = ParseSnakType(SnakType),
            Property = PropertyId,
            Hash = Hash,
            DataType = DataType.Name,
            DataValue = RawDataValue,
        };
    }

}

/// <summary>
/// Indicates the presence of value in a snak.
/// </summary>
public enum SnakType
{

    /// <summary>Custom value.</summary>
    Value = 0,

    /// <summary>Unknown value.</summary>
    SomeValue,

    /// <summary>No value.</summary>
    NoValue,

}
