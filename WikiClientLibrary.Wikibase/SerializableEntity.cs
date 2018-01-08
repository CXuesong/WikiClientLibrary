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
        }

        /// <inheritdoc />
        public string Id { get; set; }

        /// <inheritdoc />
        public WikibaseDataType DataType { get; set; }

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

        internal static EntityType ParseEntityType(string value)
        {
            switch (value)
            {
                case "item":
                    return EntityType.Item;
                case "property":
                    return EntityType.Property;
                default:
                    return EntityType.Unknown;
            }
        }

        private static string ToString(EntityType value)
        {
            switch (value)
            {
                case EntityType.Item: return "item";
                case EntityType.Property: return "property";
                default: return "unknown";
            }
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
                Aliases = new WbMonolingualTextsCollection(entity.Aliases ?? Entity.emptyStringsDict),
                Descriptions = new WbMonolingualTextCollection(entity.Descriptions ?? Entity.emptyStringDict),
                Labels = new WbMonolingualTextCollection(entity.Labels ?? Entity.emptyStringDict),
                Claims = new ClaimCollection(entity.Claims ?? Entity.emptyClaims),
                SiteLinks = new EntitySiteLinkCollection(entity.SiteLinks ?? Entity.emptySiteLinks)
            };
            return inst;
        }

        internal static SerializableEntity Load(Contracts.Entity entity)
        {
            // If caller deserializes entity from EOF-ed stream, the caller can get null.
            if (entity == null) return null;
            var inst = new SerializableEntity(false)
            {
                Id = entity.Id,
                Type = ParseEntityType(entity.Type)
            };
            if (entity.DataType != null)
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
                entity.Sitelinks?.Values.Select(v => new EntitySiteLink(v.Site, v.Title,
                    new ReadOnlyCollection<string>(v.Badges)))
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
        /// Creates a new instance of <see cref="SerializableEntity"/> from JSON.
        /// </summary>
        /// <param name="entity">The serialized entity JSON.</param>
        public static SerializableEntity Load(JObject entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return Load(entity.ToObject<Contracts.Entity>(Utility.WikiJsonSerializer));
        }

        /// <summary>
        /// Populates the current entity from JSON string.
        /// </summary>
        /// <param name="entityJson">The serialized entity JSON string.</param>
        public static SerializableEntity Load(string entityJson)
        {
            if (entityJson == null) throw new ArgumentNullException(nameof(entityJson));
            return Load(Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(entityJson));
        }


        /// <summary>
        /// Populates the current entity from <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">The reader from which to read serialized entity JSON.</param>
        /// <returns>The deserialized entity, or <c>null</c> if EOF has been reached.</returns>
        public static SerializableEntity Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            return Load(Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(reader));
        }

        /// <summary>
        /// Populates the current entity from <see cref="JsonReader"/>.
        /// </summary>
        /// <param name="reader">The reader from which to read serialized entity JSON.</param>
        /// <returns>The deserialized entity, or <c>null</c> if EOF has been reached.</returns>
        public static SerializableEntity Load(JsonReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            return Load(Utility.WikiJsonSerializer.Deserialize<Contracts.Entity>(reader));
        }

        private static IDictionary<string, Contracts.MonolingualText> ToContract(WbMonolingualTextCollection value)
        {
            return value?.ToDictionary(p => p.Language, p => new Contracts.MonolingualText {Language = p.Language, Value = p.Text});
        }

        private static IDictionary<string, ICollection<Contracts.MonolingualText>> ToContract(WbMonolingualTextsCollection value)
        {
            return value?.Languages.ToDictionary(lang => lang,
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
