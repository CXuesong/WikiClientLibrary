using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using WikiClientLibrary.Sites;
using Newtonsoft.Json.Linq;

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

        public static WikibaseSiteInfo FromSiteInfo(SiteInfo siteInfo)
        {
            JToken LoadProperty(string name)
            {
                if (siteInfo.ExtensionData.TryGetValue(name, out var v)) return v;
                return null;
            }

            if (siteInfo == null) throw new ArgumentNullException(nameof(siteInfo));
            var inst = new WikibaseSiteInfo
            {
                ConceptBaseUri = (string)LoadProperty("wikibase-conceptbaseuri"),
                GeoShapeStorageBaseUri = (string)LoadProperty("wikibase-geoshapestoragebaseurl"),
                TabularDataStorageBaseUri = (string)LoadProperty("wikibase-tabulardatastoragebaseurl")
            };
            return inst;
        }

        public string ConceptBaseUri { get; private set; }

        public string GeoShapeStorageBaseUri { get; private set; }

        public string TabularDataStorageBaseUri { get; private set; }

        public string MakeEntityUri(string entityId)
        {
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            return ConceptBaseUri + entityId;
        }

        public string ParseEntityId(string entityUri)
        {
            if (entityUri == null) throw new ArgumentNullException(nameof(entityUri));
            if (entityUri.StartsWith(ConceptBaseUri, StringComparison.Ordinal))
                return entityUri.Substring(ConceptBaseUri.Length);
            throw new ArgumentException("Cannot parse entity ID from the specified entity URI.");
        }

    }
}
