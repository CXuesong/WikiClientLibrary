using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using WikiClientLibrary.Sites;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikiClientLibrary.Wikibase
{

    /// <summary>
    /// Contains read-only data about a Wikibase-enabled site.
    /// </summary>
    public class WikibaseSiteInfo
    {

        private WikibaseSiteInfo()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="WikibaseSiteInfo"/> from an existing <see cref="SiteInfo"/>.
        /// </summary>
        public static WikibaseSiteInfo FromSiteInfo(SiteInfo siteInfo)
        {
            JToken? LoadProperty(string name)
            {
                if (siteInfo.ExtensionData.TryGetValue(name, out var v)) return v;
                return null;
            }

            if (siteInfo == null) throw new ArgumentNullException(nameof(siteInfo));
            var inst = new WikibaseSiteInfo
            {
                ConceptBaseUri = (string)LoadProperty("wikibase-conceptbaseuri") ?? "",
                GeoShapeStorageBaseUri = (string)LoadProperty("wikibase-geoshapestoragebaseurl") ?? "",
                TabularDataStorageBaseUri = (string)LoadProperty("wikibase-tabulardatastoragebaseurl") ?? ""
            };
            return inst;
        }

        /// <summary>
        /// URI prefix of Wikibase items.
        /// </summary>
        /// <remarks>The value is <c>http://www.wikidata.org/entity/</c> for Wikidata.</remarks>
        /// <seealso cref="MakeEntityUri"/>
        /// <seealso cref="ParseEntityId"/>
        public string ConceptBaseUri { get; private set; } = "";

        /// <summary>
        /// URI prefix of <see cref="BuiltInDataTypes.GeoShape"/> URLs.
        /// </summary>
        /// <remarks>The value is <c>https://commons.wikimedia.org/wiki/</c> for Wikidata.</remarks>
        public string GeoShapeStorageBaseUri { get; private set; } = "";

        /// <summary>
        /// URI prefix of <see cref="BuiltInDataTypes.TabularData"/> URLs.
        /// </summary>
        /// <remarks>The value is <c>https://commons.wikimedia.org/wiki/</c> for Wikidata.</remarks>
        public string TabularDataStorageBaseUri { get; private set; } = "";

        /// <summary>
        /// Makes an entity URL out of the specified entity ID.
        /// </summary>
        /// <param name="entityId">Wikibase entity ID, such as <c>P13</c>, <c>Q175</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entityId"/> is <c>null</c>.</exception>
        /// <returns>The corresponding Wikibase entity URI. This URI can be used in RDF representations.</returns>
        /// <remarks>This method simply concatenates <see cref="ConceptBaseUri"/> and <paramref name="entityId"/>, and does not validate the given entity ID.</remarks>
        public string MakeEntityUri(string entityId)
        {
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            return ConceptBaseUri + entityId;
        }

        /// <summary>
        /// Extracts the entity ID out of the given entity URI.
        /// </summary>
        /// <param name="entityUri">Wikibase entity URI.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entityUri"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="entityUri"/> does not have the same URI prefix as specified in <see cref="ConceptBaseUri"/>.</exception>
        /// <returns>The corresponding Wikibase entity ID.</returns>
        public string ParseEntityId(string entityUri)
        {
            if (entityUri == null) throw new ArgumentNullException(nameof(entityUri));
            if (entityUri.StartsWith(ConceptBaseUri, StringComparison.Ordinal))
                return entityUri[ConceptBaseUri.Length..];
            throw new ArgumentException("Cannot parse entity ID from the specified entity URI.");
        }

    }
}
