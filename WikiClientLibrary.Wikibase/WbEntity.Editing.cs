using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikibase
{
    partial class WbEntity
    {

        private JObject EditEntriesToJObject(IEnumerable<WbEntityEditEntry> edits)
        {
            if (edits == null) throw new ArgumentNullException(nameof(edits));
            var jdata = new JObject();
            foreach (var prop in edits.GroupBy(e => e.PropertyName))
            {
                if (prop.Any(p => p.Value == null))
                    throw new ArgumentException($"Detected null value in {prop} entries.", nameof(edits));
                switch (prop.Key)
                {
                    case nameof(Labels):
                    case nameof(Descriptions):
                        jdata.Add(prop.Key.ToLowerInvariant(),
                            prop.GroupBy(e => ((WbMonolingualText)e.Value).Language)
                                .ToJObject(g => g.Key, g =>
                                {
                                    var obj = new JObject {{"language", g.Key}};
                                    var item = g.Single();
                                    if (item.State == WbEntityEditEntryState.Updated)
                                        obj.Add("value", ((WbMonolingualText)item.Value).Text);
                                    else
                                        obj.Add("removed", "");
                                    return obj;
                                }));
                        break;
                    case nameof(Aliases):
                        jdata.Add("aliases",
                            prop.GroupBy(e => ((WbMonolingualText)e.Value).Language)
                                .ToJObject(g => g.Key, g => g.Select(item =>
                                {
                                    var obj = new JObject
                                    {
                                        {"language", g.Key},
                                        {"value", ((WbMonolingualText)item.Value).Text}
                                    };
                                    if (item.State == WbEntityEditEntryState.Removed)
                                        obj.Add("removed", "");
                                    return obj;
                                }).ToJArray()));
                        break;
                    case nameof(SiteLinks):
                        jdata.Add("sitelinks",
                            prop.GroupBy(e => ((WbEntitySiteLink)e.Value).Site)
                                .ToJObject(g => g.Key, g => g.Select(item =>
                                {
                                    var obj = new JObject {{"site", g.Key}};
                                    if (item.State == WbEntityEditEntryState.Updated)
                                    {
                                        obj.Add("title", ((WbEntitySiteLink)item.Value).Title);
                                        obj.Add("badges", ((WbEntitySiteLink)item.Value).Badges.ToJArray());
                                    }
                                    else
                                    {
                                        obj.Add("removed", "");
                                    }
                                    return obj;
                                }).ToJArray()));
                        break;
                    case nameof(Claims):
                        jdata.Add("claims",
                            prop.GroupBy(e =>
                            {
                                var pid = ((WbClaim)e.Value).MainSnak?.PropertyId;
                                if (pid == null)
                                    throw new ArgumentException("Detected null PropertyId in WbClaim values.", nameof(edits));
                                return pid;
                            }).ToJObject(g => g.Key, g => g.Select(item =>
                            {
                                if (item.State == WbEntityEditEntryState.Updated)
                                    return ((WbClaim)item.Value).ToJson(false);
                                var obj = ((WbClaim)item.Value).ToJson(true);
                                obj.Add("removed", "");
                                return obj;
                            }).ToJArray()));
                        break;
                    default:
                        throw new ArgumentException("Unrecognized WbEntity property name: " + prop.Key + ".");
                }
            }
            return jdata;
        }

        public async Task Edit(IEnumerable<WbEntityEditEntry> edits,
            string summary, bool isBot, bool clearData, CancellationToken cancellationToken)
        {
            if (edits == null) throw new ArgumentNullException(nameof(edits));
            cancellationToken.ThrowIfCancellationRequested();
            var jdata = EditEntriesToJObject(edits);
            var jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "wbeditentity",
                token = WikiSiteToken.Edit,
                id = Id,
                @new = Id == null ? Type : null,
                baserevid = LastRevisionId > 0 ? (int?)LastRevisionId : null,
                bot = isBot,
                summary = summary,
                clear = clearData,
                data = jdata
            }), cancellationToken);
            var jentity = jresult["entity"];
            if (jentity == null)
                throw new UnexpectedDataException("Missing \"entity\" node in the JSON response.");
            LoadFromJson(jresult["entity"], EntityQueryOptions.FetchAllProperties, true);
        }

    }
}
