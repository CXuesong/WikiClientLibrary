using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
                    case nameof(DataType):
                        jdata.Add("datatype", ((WbPropertyType)prop.Last().Value).Name);
                        break;
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
                                        obj.Add("remove", "");
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
                                        obj.Add("remove", "");
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
                                        obj.Add("remove", "");
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
                                obj.Add("remove", "");
                                return obj;
                            }).ToJArray()));
                        break;
                    default:
                        throw new ArgumentException("Unrecognized WbEntity property name: " + prop.Key + ".");
                }
            }
            return jdata;
        }

        /// <inheritdoc cref="EditAsync"/>
        public Task EditAsync(IEnumerable<WbEntityEditEntry> edits, string summary)
        {
            return EditAsync(edits, summary, false);
        }

        /// <inheritdoc cref="EditAsync"/>
        public Task EditAsync(IEnumerable<WbEntityEditEntry> edits, string summary, bool isBot)
        {
            return EditAsync(edits, summary, isBot, false);
        }

        /// <inheritdoc cref="EditAsync"/>
        public Task EditAsync(IEnumerable<WbEntityEditEntry> edits, string summary, bool isBot, bool clearData)
        {
            return EditAsync(edits, summary, isBot, clearData, CancellationToken.None);
        }

        /// <summary>
        /// Makes the specified changes to the current entity on the Wikibase site.
        /// </summary>
        /// <param name="edits">The changes to be made.</param>
        /// <param name="summary">The edit summary.</param>
        /// <param name="isBot">Whether to mark the edit as bot edit.</param>
        /// <param name="clearData">Whether to clear all the existing data of the entity before making the changes.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <exception cref="OperationConflictException">Edit conflict detected.</exception>
        /// <exception cref="UnauthorizedOperationException">You have no rights to edit the page.</exception>
        /// <remarks>After the operation, the entity will be automatically refereshed,
        /// which means all the <see cref="WbClaim"/> instances that used to belong to this claim will be detached,
        /// and perhaps replicates will take the place.
        /// This is effectively a refresh operation with <see cref="WbEntityQueryOptions.FetchAllProperties"/> flag,
        /// except that some properties in the <see cref="WbEntityQueryOptions.FetchInfo"/> category are just invalidated
        /// due to insufficient data contained in the MW API. (e.g. <see cref="PageId"/>) As for the properties that are
        /// affected by the edit operation, see the "remarks" section of the properties, respectively.
        /// </remarks>
        public async Task EditAsync(IEnumerable<WbEntityEditEntry> edits,
            string summary, bool isBot, bool clearData, CancellationToken cancellationToken)
        {

            string FormatEntityType(WbEntityType type)
            {
                switch (type)
                {
                    case WbEntityType.Item: return "item";
                    case WbEntityType.Property: return "property";
                    default: return "unknown";
                }
            }

            if (edits == null) throw new ArgumentNullException(nameof(edits));
            cancellationToken.ThrowIfCancellationRequested();
            var jdata = EditEntriesToJObject(edits);
            var jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "wbeditentity",
                token = WikiSiteToken.Edit,
                id = Id,
                @new = Id == null ? FormatEntityType(Type) : null,
                baserevid = LastRevisionId > 0 ? (int?)LastRevisionId : null,
                bot = isBot,
                summary = summary,
                clear = clearData,
                data = jdata.ToString(Formatting.None)
            }), cancellationToken);
            var jentity = jresult["entity"];
            if (jentity == null)
                throw new UnexpectedDataException("Missing \"entity\" node in the JSON response.");
            LoadFromJson(jresult["entity"], WbEntityQueryOptions.FetchAllProperties, true);
            Logger.LogInformation("Edited {Entity} on {Site}. New revid={RevisionId}", this, Site, LastRevisionId);
        }

    }
}
