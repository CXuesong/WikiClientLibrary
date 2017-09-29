using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikibase
{
    internal static class WikibaseRequestHelper
    {

        private static readonly Dictionary<EntityQueryOptions, string> propdict = new Dictionary<EntityQueryOptions, string>();

        private static IDictionary<string, object> BuildQueryOptions(string languages, EntityQueryOptions options)
        {
            var propValue = options & EntityQueryOptions.FetchAllProperties;
            string props;
            if (propValue == EntityQueryOptions.None)
            {
                props = "";
            }
            else
            {
                lock (propdict)
                {
                    if (!propdict.TryGetValue(propValue, out props))
                    {
                        props = null;
                        if ((propValue & EntityQueryOptions.FetchInfo) == EntityQueryOptions.FetchInfo) props += "|info";
                        if ((propValue & EntityQueryOptions.FetchLabels) == EntityQueryOptions.FetchLabels) props += "|labels";
                        if ((propValue & EntityQueryOptions.FetchAliases) == EntityQueryOptions.FetchAliases) props += "|aliases";
                        if ((propValue & EntityQueryOptions.FetchDescriptions) == EntityQueryOptions.FetchDescriptions) props += "|descriptions";
                        if ((propValue & EntityQueryOptions.FetchSiteLinks) == EntityQueryOptions.FetchSiteLinks) props += "|sitelinks";
                        if ((propValue & EntityQueryOptions.FetchSiteLinksUrl) == EntityQueryOptions.FetchSiteLinksUrl) props += "|sitelinks/urls";
                        Debug.Assert(props != null);
                        props = props.Substring(1);
                        propdict.Add(propValue, props);
                    }
                }
            }
            return new Dictionary<string, object>
            {
                {
                    "redirects",
                    (options & EntityQueryOptions.SupressRedirects) == EntityQueryOptions.SupressRedirects
                        ? "no"
                        : "yes"
                },
                {"languages", languages},
                {"props", props},
            };
        }

        public static async Task RefreshEntitiesAsync(IEnumerable<Entity> entities, EntityQueryOptions options, 
            IEnumerable<string> languages, CancellationToken cancellationToken)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var langs = languages == null ? null : string.Join("|", languages);
            if (string.IsNullOrEmpty(langs)) langs = null;
            // You can even fetch pages from different sites.
            foreach (var siteEntities in entities.GroupBy(p => p.Site))
            {
                var site = siteEntities.Key;
                var req = BuildQueryOptions(langs, options);
                req["action"] = "wbgetentities";
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits) ? 500 : 50;
                foreach (var partition in siteEntities.Partition(titleLimit).Select(partition => partition.ToList()))
                {
                    site.Logger.LogDebug("Fetching {Count} pages from {Site}.", partition.Count, site);
                    // We use ids to query pages.
                    req["ids"] = string.Join("|", partition.Select(p => p.Id));
                    var jresult = await site.GetJsonAsync(new WikiFormRequestMessage(req), cancellationToken);
                    var jentities = (JObject) jresult["entities"];
                    foreach (var entity in partition)
                    {
                        var jentity = jentities[entity.Id];
                        // We can write Q123456 as q123456 in query params, but server will return Q123456 anyway.
                        if (jentity == null)
                            jentity = jentities.Properties().FirstOrDefault(p =>
                                string.Equals(p.Name, entity.Id, StringComparison.OrdinalIgnoreCase));
                        if (jentity == null)
                            throw new UnexpectedDataException($"Cannot find the entity with id {entity.Id} in the response.");
                        entity.LoadFromJson(jentity, options);
                    }
                }
            }
        }

    }
}
