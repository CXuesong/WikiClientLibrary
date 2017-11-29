using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Wikibase.DataTypes;
using WikiClientLibrary.Wikibase.Infrastructures;

namespace WikiClientLibrary.Wikibase
{

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
        /// Initializes a new <see cref="Claim"/> instance with the specified main snak.
        /// </summary>
        /// <param name="mainSnakPropertyId">The property ID of the auto-constructed main snak.</param>
        /// <exception cref="ArgumentNullException"><paramref name="mainSnakPropertyId"/> is <c>null</c>.</exception>
        public Claim(string mainSnakPropertyId)
        {
            if (mainSnakPropertyId == null) throw new ArgumentNullException(nameof(mainSnakPropertyId));
            MainSnak = new Snak(mainSnakPropertyId);
        }
        
        /// <summary>
        /// Gets the main snak of the claim.
        /// </summary>
        /// <remarks>This property is read-only; however you can change its content exception <see cref="Snak.PropertyId"/> value.</remarks>
        public Snak MainSnak { get; }

        /// <summary>Claim ID.</summary>
        public string Id { get; set; }

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

        internal static IEnumerable<T> ToOrderedList<T>(JObject dict, JArray order, Func<JToken, IEnumerable<T>> valueSelector)
        {
            // Before #84516 Wikibase did not implement snaks-order.
            // https://gerrit.wikimedia.org/r/#/c/84516/
            Debug.Assert(dict != null);
            if (order == null)
                return dict.Values().SelectMany(valueSelector).ToList();
            Debug.Assert(order.Count == dict.Count);
            return order.Select(key => dict[(string)key]).SelectMany(valueSelector);
        }

        internal static Claim FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new Claim(Snak.FromJson(claim["mainsnak"]));
            inst.LoadFromJson(claim);
            return inst;
        }

        private void LoadFromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            Id = (string)claim["id"];
            Type = (string)claim["type"];
            Rank = (string)claim["rank"];
            _Qualifiers.Clear();
            _References.Clear();
            if (claim["qualifiers"] != null)
            {
                _Qualifiers.AddRange(ToOrderedList((JObject)claim["qualifiers"], (JArray)claim["qualifiers-order"],
                    jsnaks => jsnaks.Select(Snak.FromJson)));
            }
            if (claim["references"] != null)
            {
                _References.AddRange(claim["references"]?.Select(ClaimReference.FromJson));
            }
        }

        internal JObject ToJson(bool identifierOnly)
        {
            var obj = new JObject
            {
                {"type", Type},
                {"rank", Rank}
            };
            if (Id != null) obj.Add("id", Id);
            if (identifierOnly) return obj;
            if (MainSnak == null) throw new InvalidOperationException("MainSnak should not be null.");
            obj.Add("mainsnak", MainSnak.ToJson());
            if (Qualifiers != null)
            {
                obj.Add("qualifiers",
                    Qualifiers.GroupBy(s => s.PropertyId).ToJObject(g => g.Key, g => g.Select(s => s.ToJson()).ToJArray())
                );
                obj.Add("qualifiers-order", Qualifiers.Select(s => s.PropertyId).Distinct().ToJArray());
            }
            if (References != null)
            {
                obj.Add("references", References.Select(r => r.ToJson()).ToJArray());
            }
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

        public string Hash { get; set; }

        internal static ClaimReference FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new ClaimReference();
            inst.LoadFromJson(claim);
            return inst;
        }

        internal void LoadFromJson(JToken reference)
        {
            Debug.Assert(reference != null);
            _Snaks.Clear();
            if (reference["snaks"] != null)
            {
                _Snaks.AddRange(Claim.ToOrderedList((JObject)reference["snaks"], (JArray)reference["snaks-order"],
                    jsnaks => jsnaks.Select(Snak.FromJson)));
            }
            Hash = (string)reference["hash"];
        }

        internal JObject ToJson()
        {
            var obj = new JObject();
            if (Hash != null) obj.Add("hash", Hash);
            if (Snaks != null)
            {
                obj.Add("snaks",
                    Snaks.GroupBy(s => s.PropertyId).ToJObject(g => g.Key, g => g.Select(s => s.ToJson()).ToJArray())
                );
                obj.Add("snaks-order", Snaks.Select(s => s.PropertyId).Distinct().ToJArray());
            }
            return obj;
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

        private static readonly JToken DirtyDataValuePlaceholder = JValue.CreateNull();

        private object _DataValue;
        private JToken _RawDataValue;

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
        /// Initializes a snak with specified property ID and data value.
        /// </summary>
        /// <param name="propertyId">The property id.</param>
        /// <param name="rawDataValue">The raw JSON data value of the property.</param>
        /// <param name="dataType">The data value type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyId"/> or <paramref name="dataType"/> is <c>null</c>.</exception>
        public Snak(string propertyId, JToken rawDataValue, WikibaseDataType dataType)
        {
            PropertyId = propertyId ?? throw new ArgumentNullException(nameof(propertyId));
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            RawDataValue = rawDataValue;
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
        public string Hash { get; set; }

        /// <summary>Raw JSON value of <c>datavalue</c> node.</summary>
        public JToken RawDataValue
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
                var data = new JObject
                {
                    {"value", DataType.ToJson(_DataValue)},
                    {"type", DataType.ValueTypeName}
                };
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
        public object DataValue
        {
            get
            {
                if (_DataValue != DirtyDataValuePlaceholder) return _DataValue;
                if (_RawDataValue == null)
                {
                    _DataValue = null;
                    return null;
                }
                Debug.Assert(_RawDataValue != DirtyDataValuePlaceholder);
                if (DataType == null) throw new InvalidOperationException("DataType is null.");
                var valueType = (string)RawDataValue["type"];
                if (valueType != null && valueType != DataType.ValueTypeName)
                    throw new NotSupportedException($"Parsing value type \"{valueType}\" is not supported by {DataType}.");
                var value = DataType.Parse(_RawDataValue["value"]);
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
        public WikibaseDataType DataType { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            // TODO need something link TryGetDataValue to handle unknown data types
            var valueExpr = "[Invalid SnakType]";
            switch (SnakType)
            {
                case SnakType.Value:
                    valueExpr = DataValue?.ToString();
                    break;
                case SnakType.SomeValue:
                    valueExpr = "[SomeValue]";
                    break;
                case SnakType.NoValue:
                    valueExpr = "[NoValue]";
                    break;
            }
            return $"{PropertyId} = {valueExpr}";
        }

        private static SnakType ParseSnakType(string expr)
        {
            if (expr == null) throw new ArgumentNullException(nameof(expr));
            switch (expr)
            {
                case "value": return SnakType.Value;
                case "somevalue": return SnakType.SomeValue;
                case "novalue": return SnakType.NoValue;
                default: throw new ArgumentException("Invalid SnackType expression.", nameof(expr));
            }
        }

        private static string ParseSnakType(SnakType value)
        {
            switch (value)
            {
                case SnakType.Value: return "value";
                case SnakType.SomeValue: return "somevalue";
                case SnakType.NoValue: return "novalue";
                default: throw new ArgumentException("Invalid SnackType value.", nameof(value));
            }
        }

        internal static Snak FromJson(JToken snak)
        {
            Debug.Assert(snak != null);
            var inst = new Snak((string)snak["property"]);
            inst.LoadFromJson(snak);
            return inst;
        }

        private void LoadFromJson(JToken snak)
        {
            Debug.Assert(snak != null);
            SnakType = ParseSnakType((string)snak["snaktype"]);
            Hash = (string)snak["hash"];
            RawDataValue = snak["datavalue"];
            DataType = BuiltInDataTypes.Get((string)snak["datatype"])
                       ?? MissingPropertyType.Get((string)snak["datatype"], (string)snak["value"]?["type"]);
        }

        internal JObject ToJson()
        {
            var obj = new JObject
            {
                {"snaktype", ParseSnakType(SnakType)},
                {"property", PropertyId},
                {"hash", Hash},
                {"datatype", DataType?.Name},
                {"datavalue", RawDataValue}
            };
            return obj;
        }

    }

    public enum SnakType
    {
        /// <summary>Custom value.</summary>
        Value = 0,

        /// <summary>Unknown value.</summary>
        SomeValue,

        /// <summary>No value.</summary>
        NoValue,
    }

}

