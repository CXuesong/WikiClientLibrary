using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Wikibase
{
    
    public sealed class WikibaseClaim
    {
       
        public WikibaseSnak MainSnak { get; set; }
        
        /// <summary>Claim ID.</summary>
        public string Id { get; set; }

        /// <summary>Rank of the claim.</summary>
        /// <remarks>The value often is <c>normal</c>.</remarks>
        public string Rank { get; set; }

        public IList<WikibaseSnak> Qualifiers { get; set; }

        public IList<WikibaseClaimReference> References { get; set; }

        internal static List<T> ToOrderedList<T>(JObject dict, JArray order, Func<JToken, T> valueSelector)
        {
            if (order == null)
                return dict.Values().Select(valueSelector).ToList();
            Debug.Assert(order.Count == dict.Count);
            return order.Select(key => dict[(string) key]).Select(valueSelector).ToList();
        }

        internal static WikibaseClaim FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new WikibaseClaim();
            inst.LoadFromJson(claim);
            return inst;
        }

        internal void LoadFromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            MainSnak = claim["mainsnak"] == null ? null : WikibaseSnak.FromJson(claim["mainsnak"]);
            Id = (string) claim["id"];
            Rank = (string) claim["rank"];
            Qualifiers = ToOrderedList((JObject) claim["qualifiers"], (JArray) claim["qualifiers-order"], WikibaseSnak.FromJson);
            References = claim["references"]?.Select(WikibaseClaimReference.FromJson).ToList();
        }

    }

    public sealed class WikibaseClaimReference
    {

        public IList<WikibaseSnak> Snaks { get; set; }
        
        public string Hash { get; set; }

        internal static WikibaseClaimReference FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new WikibaseClaimReference();
            inst.LoadFromJson(claim);
            return inst;
        }

        internal void LoadFromJson(JToken reference)
        {
            Debug.Assert(reference != null);
            Snaks = WikibaseClaim.ToOrderedList((JObject) reference["snaks"], (JArray) reference["snaks-order"], WikibaseSnak.FromJson);
            Hash = (string) reference["hash"];
        }

    }

    public sealed class WikibaseSnak
    {

        private static readonly JToken DirtyDataValuePlaceholder = JValue.CreateNull();

        private object _DataValue;
        private JToken _RawDataValue;
        
        /// <summary>Snak type.</summary>
        public SnakType SnakType { get; set; }

        /// <summary>Property ID, with "P" prefix.</summary>
        public string PropertyId { get; set; }

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
                var valueType = (string) RawDataValue["type"];
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
        public PropertyType DataType { get; set; }

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

        internal static WikibaseSnak FromJson(JToken claim)
        {
            Debug.Assert(claim != null);
            var inst = new WikibaseSnak();
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
            DataType = PropertyTypes.Get((string)snak["datatype"])
                       ?? MissingPropertyType.Get((string)snak["datatype"]);
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
