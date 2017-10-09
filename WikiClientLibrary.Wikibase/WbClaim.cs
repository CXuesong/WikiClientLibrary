using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Wikibase.Infrastructures;

namespace WikiClientLibrary.Wikibase
{

    public sealed class WbClaim
    {

        internal event EventHandler<KeyChangingEventArgs> KeyChanging;
        private WbSnak _MainSnak;

        public WbSnak MainSnak
        {
            get { return _MainSnak; }
            set
            {
                if (_MainSnak != value)
                {
                    var oldKey = _MainSnak?.PropertyId;
                    if (KeyChanging != null && oldKey != value?.PropertyId)
                        KeyChanging(this, new KeyChangingEventArgs(value?.PropertyId));
                    if (_MainSnak != null) _MainSnak.KeyChanging -= MainSnak_KeyChanging;
                    _MainSnak = value;
                    if (value != null) value.KeyChanging += MainSnak_KeyChanging;
                }
            }
        }

        private void MainSnak_KeyChanging(object sender, KeyChangingEventArgs keyChangingEventArgs)
        {
            KeyChanging?.Invoke(this, keyChangingEventArgs);
        }

        /// <summary>Claim ID.</summary>
        public string Id { get; set; }

        /// <summary>The type of the claim.</summary>
        /// <remarks>The value often is <c>statement</c>.</remarks>
        public string Type { get; set; } = "statement";

        /// <summary>Rank of the claim.</summary>
        /// <remarks>The value often is <c>normal</c>.</remarks>
        public string Rank { get; set; } = "normal";

        public IList<WbSnak> Qualifiers { get; set; }

        public IList<WbClaimReference> References { get; set; }

        internal static List<T> ToOrderedList<T>(JObject dict, JArray order, Func<JToken, IEnumerable<T>> valueSelector)
        {
            // Before #84516 Wikibase did not implement snaks-order.
            // https://gerrit.wikimedia.org/r/#/c/84516/
            Debug.Assert(dict != null);
            if (order == null)
                return dict.Values().SelectMany(valueSelector).ToList();
            Debug.Assert(order.Count == dict.Count);
            return order.Select(key => dict[(string)key]).SelectMany(valueSelector).ToList();
        }

        internal static WbClaim FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new WbClaim();
            inst.LoadFromJson(claim);
            return inst;
        }

        internal void LoadFromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            MainSnak = claim["mainsnak"] == null ? null : WbSnak.FromJson(claim["mainsnak"]);
            Id = (string)claim["id"];
            Type = (string)claim["type"];
            Rank = (string)claim["rank"];
            Qualifiers = claim["qualifiers"] == null
                ? null
                : ToOrderedList((JObject)claim["qualifiers"], (JArray)claim["qualifiers-order"],
                    jsnaks => jsnaks.Select(WbSnak.FromJson));
            References = claim["references"]?.Select(WbClaimReference.FromJson).ToList();
        }

        internal JObject ToJson(bool identifierOnly)
        {
            var obj = new JObject
            {
                {"id", Id},
                {"type", Type},
                {"rank", Rank}
            };
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

    public sealed class WbClaimReference
    {

        public IList<WbSnak> Snaks { get; set; }

        public string Hash { get; set; }

        internal static WbClaimReference FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new WbClaimReference();
            inst.LoadFromJson(claim);
            return inst;
        }

        internal void LoadFromJson(JToken reference)
        {
            Debug.Assert(reference != null);
            Snaks = reference["snaks"] == null
                ? null
                : WbClaim.ToOrderedList((JObject)reference["snaks"], (JArray)reference["snaks-order"],
                    jsnaks => jsnaks.Select(WbSnak.FromJson));
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

    public sealed class WbSnak
    {

        private static readonly JToken DirtyDataValuePlaceholder = JValue.CreateNull();

        private object _DataValue;
        private JToken _RawDataValue;

        internal event EventHandler<KeyChangingEventArgs> KeyChanging;
        private string _PropertyId;

        /// <summary>Snak type.</summary>
        public SnakType SnakType { get; set; }

        /// <summary>Property ID, with "P" prefix.</summary>
        public string PropertyId
        {
            get { return _PropertyId; }
            set
            {
                if (_PropertyId != value)
                {
                    KeyChanging?.Invoke(this, new KeyChangingEventArgs(value));
                    _PropertyId = value;
                }
            }
        }

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
        public WbPropertyType DataType { get; set; }

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

        internal static WbSnak FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new WbSnak();
            inst.LoadFromJson(claim);
            return inst;
        }

        internal void LoadFromJson(JToken snak)
        {
            Debug.Assert(snak != null);
            SnakType = ParseSnakType((string)snak["snaktype"]);
            PropertyId = (string)snak["property"];
            Hash = (string)snak["hash"];
            RawDataValue = snak["datavalue"];
            DataType = WbPropertyTypes.Get((string)snak["datatype"])
                       ?? MissingPropertyType.Get((string)snak["datatype"], (string)snak["value"]?["type"]);
        }

        internal JObject ToJson()
        {
            var obj = new JObject
            {
                {"snaktype", ParseSnakType(SnakType)},
                {"property", PropertyId},
                {"hash", Hash},
                {"datatype", DataType.Name},
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

