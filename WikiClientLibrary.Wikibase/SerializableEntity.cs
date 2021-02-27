using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// Represents a entity that can be loaded from or serialized into JSON.
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
        /// Creates a new instance of <see cref="SerializableEntity"/> from <see cref="JObject"/>.
        /// </summary>
        /// <param name="entity">The serialized entity JSON.</param>
        public static SerializableEntity Load(JObject entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return Load(entity.ToObject<Contracts.Entity>(Utility.WikiJsonSerializer));
        }

        /// <summary>
        /// Loads 1 <see cref="SerializableEntity"/> from text reader.
        /// </summary>
        /// <param name="reader">The reader from which to read serialized entity JSON.</param>
        /// <returns>The deserialized entity, or <c>null</c> if EOF has been reached.</returns>
        public static SerializableEntity? Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            var contract = Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(reader);
            return contract == null ? null : Load(contract);
        }

        /// <summary>
        /// Loads 1 <see cref="SerializableEntity"/> from JSON reader.
        /// </summary>
        /// <param name="reader">The reader from which to read serialized entity JSON.</param>
        /// <returns>The deserialized entity, or <c>null</c> if EOF has been reached.</returns>
        public static SerializableEntity? Load(JsonReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            var contract = Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(reader);
            return contract == null ? null : Load(contract);
        }

        /// <inheritdoc cref="LoadAll(JsonReader)"/>
        /// <summary>
        /// Enumerates all the entities from JSON array of serialized entities contained in the string.
        /// </summary>
        /// <param name="entitiesJson">The serialized entities JSON string.</param>
        /// <remarks></remarks>
        public static IEnumerable<SerializableEntity?> ParseAll(string entitiesJson)
        {
            if (entitiesJson == null) throw new ArgumentNullException(nameof(entitiesJson));
            using var sr = new StringReader(entitiesJson);
            using var jr = new JsonTextReader(sr);
            foreach (var i in LoadAll(jr)) yield return i;
        }

        /// <inheritdoc cref="LoadAll(JsonReader)"/>
        public static IEnumerable<SerializableEntity?> LoadAll(TextReader reader)
        {
            using var jr = new JsonTextReader(reader) {CloseInput = false};
            foreach (var i in LoadAll(jr)) yield return i;
        }

        /// <summary>
        /// Enumerates all the entities from JSON array of serialized contained in the JSON reader.
        /// </summary>
        /// <param name="reader">The reader from which to read serialized entity JSON.</param>
        /// <exception cref="JsonException">The JSON is invalid.</exception>
        /// <returns>
        /// A sequence that, when enumerated, will read a JSON array from reader, and parses it
        /// into a sequence of <see cref="SerializableEntity"/>s.
        /// If there is <c>null</c> in the JSON array, <c>null</c> will be enumerated in the sequence.
        /// </returns>
        /// <remarks>
        /// <para>This method is recommended when you are working with a large JSON dump of Wikibase entities,
        /// because it only put the current enumerated entity in the memory. Still, you can use
        /// <see cref="Enumerable.ToList{TSource}"/> to get all the entities at one time.</para>
        /// <para>If you stopped enumerating the returned sequence, the <paramref name="reader"/> will stop
        /// in the middle of array, at the end of current enumerated entity.</para>
        /// </remarks>
        public static IEnumerable<SerializableEntity?> LoadAll(JsonReader reader)
        {
            // Skip comments
            while (reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.Comment)
            {
                if (!reader.Read()) yield break;
            }

            if (reader.TokenType != JsonToken.StartArray) throw new JsonException("Expect StartArray token.");
            //var startOfArrayDepth = reader.Depth;
            //try
            //{
            while (reader.Read())
            {
                // Skip comments
                while (reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.Comment)
                {
                    if (!reader.Read()) yield break;
                }
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        var entity = Load(reader);
                        yield return entity;
                        break;
                    case JsonToken.EndArray:
                        yield break;
                    case JsonToken.Null:
                    case JsonToken.Undefined:
                        yield return null;
                        break;
                    default:
                        throw new JsonException($"Unexpected Json token: {reader.TokenType} at path: {reader.Path} .");
                }
            }

            //}
            //finally
            //{
            //    while (reader.TokenType != JsonToken.EndArray || reader.Depth > startOfArrayDepth)
            //    {
            //        // Fast forward to the end of array
            //        if (!reader.Read()) break;
            //    }
            //}
        }

        /// <inheritdoc cref="Load(JsonReader)"/>
        /// <summary>
        /// Loads 1 <see cref="SerializableEntity"/> from the specified file.
        /// </summary>
        /// <param name="fileName">The path of file containing the serialized JSON of a single entity.</param>
        /// <seealso cref="LoadAll(string)"/>
        public static SerializableEntity? Load(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            using var reader = File.OpenText(fileName);
            var contract = Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(reader);
            return contract == null ? null : Load(contract);
        }

        /// <inheritdoc cref="LoadAll(JsonReader)"/>
        /// <summary>
        /// Creates a new instance of <see cref="SerializableEntity"/> from JSON contained in the file.
        /// </summary>
        /// <param name="fileName">The path of file containing the serialized JSON of a single entity.</param>
        /// <remarks></remarks>
        /// <seealso cref="Load(string)"/>
        public static IEnumerable<SerializableEntity?> LoadAll(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            using var reader = File.OpenText(fileName);
            foreach (var i in LoadAll(reader))
                yield return i;
        }

        /// <summary>
        /// Creates a new instance of <see cref="SerializableEntity"/> from JSON string.
        /// </summary>
        /// <param name="entityJson">The serialized entity JSON string.</param>
        /// <returns>the first parsed entity, or <c>null</c> if the input is empty or does not contain JSON object.</returns>
        public static SerializableEntity? Parse(string entityJson)
        {
            if (entityJson == null) throw new ArgumentNullException(nameof(entityJson));
            var contract = Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(entityJson);
            return contract == null ? null : Load(contract);
        }

        private static IDictionary<string, Contracts.MonolingualText> ToContract(WbMonolingualTextCollection value)
        {
            return value.ToDictionary(p => p.Language, p => new Contracts.MonolingualText {Language = p.Language, Value = p.Text});
        }

        private static IDictionary<string, ICollection<Contracts.MonolingualText>> ToContract(WbMonolingualTextsCollection value)
        {
            return value.Languages.ToDictionary(lang => lang,
                lang => (ICollection<Contracts.MonolingualText>)value[lang]
                    .Select(t => new Contracts.MonolingualText {Language = lang, Value = t}).ToList());
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
                    link => new Contracts.SiteLink {Site = link.Site, Title = link.Title, Badges = link.Badges.ToList()}),
                Claims = Claims.GroupBy(c => c.MainSnak.PropertyId).ToDictionary(g => g.Key,
                    g => (ICollection<Contracts.Claim>)g.Select(c => c.ToContract(false)).ToList())
            };
            return obj;
        }

        /// <summary>
        /// Serializes the entity into JSON.
        /// </summary>
        public JObject ToJObject()
        {
            return JObject.FromObject(ToContract(), Utility.WikiJsonSerializer);
        }

        /// <summary>
        /// Serializes the entity into compact JSON string.
        /// </summary>
        public string ToJsonString()
        {
            return Utility.WikiJsonSerializer.Serialize(ToContract());
        }

        /// <summary>
        /// Writes the serialized entity into <see cref="JsonWriter"/>.
        /// </summary>
        public void WriteTo(JsonWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            Utility.WikiJsonSerializer.Serialize(writer, ToContract());
        }

        /// <summary>
        /// Writes the serialized entity into <see cref="TextWriter"/>.
        /// </summary>
        public void WriteTo(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            Utility.WikiJsonSerializer.Serialize(writer, ToContract());
        }

    }
}
