﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikibase;

internal static class WikibaseRequestHelper
{

    private static readonly Dictionary<EntityQueryOptions, string> propdict = new Dictionary<EntityQueryOptions, string>();

    private static IDictionary<string, object?> BuildQueryOptions(string? languages, EntityQueryOptions options)
    {
        var propValue = options & EntityQueryOptions.FetchAllProperties;
        string? props;
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
                    if ((propValue & EntityQueryOptions.FetchDescriptions) == EntityQueryOptions.FetchDescriptions)
                        props += "|descriptions";
                    if ((propValue & EntityQueryOptions.FetchSiteLinks) == EntityQueryOptions.FetchSiteLinks) props += "|sitelinks";
                    if ((propValue & EntityQueryOptions.FetchSiteLinksUrl) == EntityQueryOptions.FetchSiteLinksUrl)
                        props += "|sitelinks/urls";
                    if ((propValue & EntityQueryOptions.FetchClaims) == EntityQueryOptions.FetchClaims) props += "|claims";
                    Debug.Assert(props != null);
                    props = props[1..];
                    propdict.Add(propValue, props);
                }
            }
        }

        return new Dictionary<string, object?>
        {
            {
                "redirects", (options & EntityQueryOptions.SuppressRedirects) == EntityQueryOptions.SuppressRedirects
                    ? "no"
                    : "yes"
            },
            { "languages", languages },
            { "props", props },
        };
    }

    public static async Task RefreshEntitiesAsync(IEnumerable<Entity> entities, EntityQueryOptions options,
        IEnumerable<string>? languages, CancellationToken cancellationToken)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        var langs = languages == null ? null : MediaWikiHelper.JoinValues(languages);
        if (string.IsNullOrEmpty(langs)) langs = null;
        // You can even fetch pages from different sites.
        foreach (var siteEntities in entities.GroupBy(p => p.Site))
        {
            var site = siteEntities.Key;
            var req = BuildQueryOptions(langs, options);
            req["action"] = "wbgetentities";
            var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits) ? 500 : 50;

            using var _ = site.BeginActionScope(entities, options);
            foreach (var partition in siteEntities.Partition(titleLimit).Select(partition => partition.ToList()))
            {
                //site.Logger.LogDebug("Fetching {Count} pages from {Site}.", partition.Count, site);
                // We use ids to query pages.
                var ids = MediaWikiHelper.JoinValues(partition.Where(p => p.Id != null).Select(p => p.Id));
                // No entity to fetch
                if (ids == "") continue;
                req["ids"] = ids;
                var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(req), cancellationToken);
                var jentities = jresult["entities"].AsObject();
                foreach (var entity in partition)
                {
                    if (entity.Id == null) continue;

                    var jentity = jentities[entity.Id];
                    // We can write Q123456 as q123456 in query params, but server will return Q123456 anyway.
                    jentity ??= jentities.FirstOrDefault(p =>
                        string.Equals(p.Key, entity.Id, StringComparison.OrdinalIgnoreCase)).Value;
                    if (jentity == null)
                        throw new UnexpectedDataException($"Cannot find the entity with id {entity.Id} in the response.");
                    entity.LoadFromJson(jentity, options, false);
                }
            }
        }
    }

    private static readonly char[] whitespaceAndUnderscore = { ' ', '\t', '\v', '　', '_' };

    public static async IAsyncEnumerable<string?> EntityIdsFromSiteLinksAsync(WikiSite site,
        string siteName, IEnumerable<string> siteLinks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Debug.Assert(siteName != null);
        Debug.Assert(siteLinks != null);
        var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits) ? 500 : 50;
        var req = new OrderedKeyValuePairs<string, string>
        {
            { "action", "wbgetentities" },
            { "props", "sitelinks" },
            { "sites", siteName },
            { "sitefilter", siteName },
        };

        using var _ = site.BeginActionScope(siteLinks);
        foreach (var partition in siteLinks.Partition(titleLimit).Select(partition => partition.ToList()))
        {
            //site.Logger.LogDebug("Fetching {Count} pages from {Site}.", partition.Count, site);
            for (int i = 0; i < partition.Count; i++)
            {
                if (partition[i] == null) throw new ArgumentException("Link titles contain null element.", nameof(siteLinks));
                // Do some basic title normalization locally.
                // Note Wikibase cannot even normalize the first letter case of the title.
                partition[i] = partition[i].Trim(whitespaceAndUnderscore).Replace('_', ' ');
            }
            req["titles"] = MediaWikiHelper.JoinValues(partition);
            var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(req), cancellationToken);
            var jentities = jresult["entities"].AsObject();
            var nameIdDict = jentities.Where(p => p.Value!["missing"] == null)
                .ToDictionary(p => (string)p.Value["sitelinks"][siteName]["title"], p => (string)p.Value["id"]);
            using (ExecutionContextStash.Capture())
                foreach (var title in partition)
                    yield return nameIdDict.GetValueOrDefault(title);
        }
    }

}
