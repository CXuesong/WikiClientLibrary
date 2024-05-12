﻿using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikibase.Contracts;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikiClientLibrary.Wikibase;

partial class Entity
{

    private Contracts.Entity SerializeEditEntries(IEnumerable<EntityEditEntry> edits)
    {
        if (edits == null) throw new ArgumentNullException(nameof(edits));
        var contract = new Contracts.Entity();
        foreach (var prop in edits.GroupBy(e => e.PropertyName))
        {
            if (prop.Any(p => p.Value == null))
                throw new ArgumentException($"Detected null value in {prop} entries.", nameof(edits));
            switch (prop.Key)
            {
                case nameof(DataType):
                    var lastEntry = prop.Last();
                    Debug.Assert(lastEntry.Value != null);
                    contract.DataType = ((WikibaseDataType)lastEntry.Value).Name;
                    break;
                case nameof(Labels):
                    contract.Labels = new Dictionary<string, Contracts.MonolingualText>();
                    foreach (var entry in prop)
                    {
                        Debug.Assert(entry.Value != null);
                        var value = (WbMonolingualText)entry.Value;
                        var item = entry.State == EntityEditEntryState.Removed
                            ? new Contracts.MonolingualText {Language = value.Language, Remove = true}
                            : new Contracts.MonolingualText {Language = value.Language, Value = value.Text};
                        contract.Labels.Add(value.Language, item);
                    }
                    break;
                case nameof(Descriptions):
                    contract.Descriptions = new Dictionary<string, Contracts.MonolingualText>();
                    foreach (var entry in prop)
                    {
                        Debug.Assert(entry.Value != null);
                        var value = (WbMonolingualText)entry.Value;
                        var item = entry.State == EntityEditEntryState.Removed
                            ? new Contracts.MonolingualText { Language = value.Language, Remove = true }
                            : new Contracts.MonolingualText { Language = value.Language, Value = value.Text };
                        contract.Descriptions.Add(value.Language, item);
                    }
                    break;
                case nameof(Aliases):
                    contract.Aliases = new Dictionary<string, ICollection<MonolingualText>>();
                    foreach (var entry in prop)
                    {
                        Debug.Assert(entry.Value != null);
                        var value = (WbMonolingualText)entry.Value;
                        var item = entry.State == EntityEditEntryState.Removed
                            ? new Contracts.MonolingualText { Language = value.Language, Remove = true }
                            : new Contracts.MonolingualText { Language = value.Language, Value = value.Text };
                        if (!contract.Aliases.TryGetValue(item.Language, out var items))
                        {
                            items = new List<MonolingualText>();
                            contract.Aliases.Add(item.Language, items);
                        }
                        items.Add(item);
                    }
                    break;
                case nameof(SiteLinks):
                    contract.Sitelinks = new Dictionary<string, SiteLink>();
                    foreach (var entry in prop)
                    {
                        Debug.Assert(entry.Value != null);
                        var value = (EntitySiteLink)entry.Value;
                        var item = entry.State == EntityEditEntryState.Removed
                            ? new Contracts.SiteLink {Site = value.Site, Remove = true}
                            : new Contracts.SiteLink {Site = value.Site, Title = value.Title, Badges = value.Badges.ToList()};
                        contract.Sitelinks.Add(value.Site, item);
                    }
                    break;
                case nameof(Claims):
                    contract.Claims = new Dictionary<string, ICollection<Contracts.Claim>>();
                    foreach (var entry in prop)
                    {
                        Debug.Assert(entry.Value != null);
                        var value = (Claim)entry.Value;
                        Contracts.Claim item;
                        if (entry.State == EntityEditEntryState.Removed)
                        {
                            item = value.ToContract(true);
                            item.Remove = true;
                        }
                        else
                        {
                            item = value.ToContract(false);
                        }
                        if (!contract.Claims.TryGetValue(value.MainSnak.PropertyId, out var items))
                        {
                            items = new List<Contracts.Claim>();
                            contract.Claims.Add(value.MainSnak.PropertyId, items);
                        }
                        items.Add(item);
                    }
                    break;
                default:
                    throw new ArgumentException($"Unrecognized {nameof(Entity)} property name: {prop.Key}.");
            }
        }
        return contract;
    }

    /// <inheritdoc cref="EditAsync(IEnumerable{EntityEditEntry},string,EntityEditOptions,CancellationToken)"/>
    public Task EditAsync(IEnumerable<EntityEditEntry> edits, string summary)
    {
        return EditAsync(edits, summary, EntityEditOptions.None, CancellationToken.None);
    }

    /// <inheritdoc cref="EditAsync(IEnumerable{EntityEditEntry},string,EntityEditOptions,CancellationToken)"/>
    public Task EditAsync(IEnumerable<EntityEditEntry> edits, string summary, EntityEditOptions options)
    {
        return EditAsync(edits, summary, options, CancellationToken.None);
    }

    private static string FormatEntityType(EntityType type)
    {
        return type switch
        {
            EntityType.Item => "item",
            EntityType.Property => "property",
            _ => throw new ArgumentException("Invalid entity type.", nameof(type))
        };
    }

    private void CheckEditOptions(EntityEditOptions options)
    {
        if ((options & (EntityEditOptions.Bot | EntityEditOptions.Progressive
                                              | EntityEditOptions.Bulk | EntityEditOptions.ClearData
                                              | EntityEditOptions.StrictEditConflictDetection)) != options)
            throw new ArgumentOutOfRangeException(nameof(options));
        if ((options & EntityEditOptions.Progressive) == EntityEditOptions.Progressive
            && (options & EntityEditOptions.Bulk) == EntityEditOptions.Bulk)
            throw new ArgumentException("EntityEditOptions.Progressive and EntityEditOptions.Bulk cannot be specified at the same time.");
    }

    /// <summary>
    /// Makes the specified changes to the current entity on the Wikibase site.
    /// </summary>
    /// <param name="edits">The changes to be made.</param>
    /// <param name="summary">The edit summary.</param>
    /// <param name="options">Edit options.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="edits"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is invalid.</exception>
    /// <exception cref="NotSupportedException">Attempt to set <see cref="DataType"/> when editing an existing property entity, or force progressive edits when creating a property entity.</exception>
    /// <exception cref="OperationConflictException">Edit conflict detected.</exception>
    /// <exception cref="UnauthorizedOperationException">You have no rights to edit the page.</exception>
    /// <remarks><para>After the operation, the entity may be automatically refreshed,
    /// which means all the <see cref="Claim"/> instances that used to belong to this claim will be detached,
    /// and perhaps replicates will take the place.
    /// If the edit operation is bulk edit, this is effectively a refresh operation with <see cref="EntityQueryOptions.FetchAllProperties"/> flag,
    /// except that some properties in the <see cref="EntityQueryOptions.FetchInfo"/> category are just invalidated
    /// due to insufficient data contained in the MW API. (e.g. <see cref="PageId"/>) As for the properties that are
    /// affected by the edit operation, see the "remarks" section of the properties, respectively.</para>
    /// <para>If the edit operation is progressive edit, only the <see cref="LastRevisionId"/> is valid, after the edit operation.</para>
    /// <para>For more information about bulk edit and progressive edit, see the "remarks" section
    /// of <see cref="EntityEditOptions"/>.</para>
    /// <para>If you need more information about the entity after the edit, consider invoking <see cref="RefreshAsync()"/> again.</para>
    /// </remarks>
    public async Task EditAsync(IEnumerable<EntityEditEntry> edits, string summary, EntityEditOptions options, CancellationToken cancellationToken)
    {
        const int bulkEditThreshold = 5;
        if (edits == null) throw new ArgumentNullException(nameof(edits));
        if (Id == null && Type == EntityType.Property && (options & EntityEditOptions.Progressive) == EntityEditOptions.Progressive)
            throw new NotSupportedException("Creating a property is not possible in progressive mode.");
        CheckEditOptions(options);
        cancellationToken.ThrowIfCancellationRequested();
        using (Site.BeginActionScope(this, options))
        {
            var bulk = (options & EntityEditOptions.Bulk) == EntityEditOptions.Bulk;
            if (!bulk && (options & EntityEditOptions.Progressive) != EntityEditOptions.Progressive)
            {
                // Determine to use bulk/progressive by the item count.
                if (Id == null) bulk = true;
                else if (edits is IReadOnlyCollection<EntityEditEntry> rc) bulk = rc.Count >= bulkEditThreshold;
                else if (edits is ICollection<EntityEditEntry> c) bulk = c.Count >= bulkEditThreshold;
                else
                {
                    var items = edits.ToList();
                    bulk = items.Count >= bulkEditThreshold;
                    // Prevent multiple invocation to IEnumerable<T>.GetEnumerator()
                    edits = items;
                }
            }
            Site.Logger.LogInformation("Editing entity. Bulk={Bulk}.", bulk);
            if (bulk)
            {
                var contract = SerializeEditEntries(edits);
                using (await Site.ModificationThrottler.QueueWorkAsync("Edit: " + this, cancellationToken))
                {
                    var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "wbeditentity",
                        token = WikiSiteToken.Edit,
                        id = Id,
                        @new = Id == null ? FormatEntityType(Type) : null,
                        baserevid = LastRevisionId > 0 ? (int?)LastRevisionId : null,
                        bot = (options & EntityEditOptions.Bot) == EntityEditOptions.Bot,
                        summary = summary,
                        clear = (options & EntityEditOptions.ClearData) == EntityEditOptions.ClearData,
                        data = Utility.WikiJsonSerializer.Serialize(contract)
                    }), cancellationToken);
                    var jentity = jresult["entity"];
                    if (jentity == null)
                        throw new UnexpectedDataException("Missing \"entity\" node in the JSON response.");
                    LoadFromJson(jentity, EntityQueryOptions.FetchAllProperties, true);
                }
            }
            else
            {
                using (await Site.ModificationThrottler.QueueWorkAsync("Progressive edit: " + this, cancellationToken))
                {
                    await ProgressiveEditAsync(edits, summary,
                        (options & EntityEditOptions.Bot) == EntityEditOptions.Bot,
                        (options & EntityEditOptions.StrictEditConflictDetection) == EntityEditOptions.StrictEditConflictDetection,
                        cancellationToken);
                }
            }
            Site.Logger.LogInformation("Edited entity. New revid={RevisionId}.", LastRevisionId);
        }
    }

    private async Task ProgressiveEditAsync(IEnumerable<EntityEditEntry> edits, string summary, bool isBot,
        bool strict, CancellationToken cancellationToken)
    {
        Debug.Assert(edits != null);
        var checkbaseRev = true;
        foreach (var prop in edits.GroupBy(e => e.PropertyName))
        {
            if (prop.Any(p => p.Value == null))
                throw new ArgumentException($"Detected null value in {prop} entries.", nameof(edits));
            switch (prop.Key)
            {
                case nameof(DataType):
                    throw new NotSupportedException("Setting data type is not possible in progressive mode.");
                case nameof(Labels):
                    foreach (var p in prop)
                    {
                        Debug.Assert(p.Value != null);
                        var value = (WbMonolingualText)p.Value;
                        var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                        {
                            action = "wbsetlabel",
                            token = WikiSiteToken.Edit,
                            id = Id,
                            @new = Id == null ? FormatEntityType(Type) : null,
                            baserevid = checkbaseRev && LastRevisionId > 0 ? (int?)LastRevisionId : null,
                            bot = isBot,
                            summary = summary,
                            language = value.Language,
                            value = p.State == EntityEditEntryState.Updated ? value.Text : null,
                        }), cancellationToken);
                        LoadEntityMinimal(jresult["entity"]);
                        if (!strict) checkbaseRev = false;
                    }
                    break;
                case nameof(Descriptions):
                    foreach (var p in prop)
                    {
                        Debug.Assert(p.Value != null);
                        var value = (WbMonolingualText)p.Value;
                        var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                        {
                            action = "wbsetdescription",
                            token = WikiSiteToken.Edit,
                            id = Id,
                            @new = Id == null ? FormatEntityType(Type) : null,
                            baserevid = checkbaseRev && LastRevisionId > 0 ? (int?)LastRevisionId : null,
                            bot = isBot,
                            summary = summary,
                            language = value.Language,
                            value = p.State == EntityEditEntryState.Updated ? value.Text : null,
                        }), cancellationToken);
                        LoadEntityMinimal(jresult["entity"]);
                        if (!strict) checkbaseRev = false;
                    }
                    break;
                case nameof(Aliases):
                    {
                        var entries = prop.GroupBy(t => ((WbMonolingualText)t.Value!).Language);
                        foreach (var langGroup in entries)
                        {
                            var addExpr = MediaWikiHelper.JoinValues(langGroup
                                .Where(e => e.State == EntityEditEntryState.Updated)
                                .Select(e => ((WbMonolingualText)e.Value!).Text));
                            var removeExpr = MediaWikiHelper.JoinValues(langGroup
                                .Where(e => e.State == EntityEditEntryState.Removed)
                                .Select(e => ((WbMonolingualText)e.Value!).Text));
                            var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                            {
                                action = "wbsetaliases",
                                token = WikiSiteToken.Edit,
                                id = Id,
                                @new = Id == null ? FormatEntityType(Type) : null,
                                baserevid = checkbaseRev && LastRevisionId > 0 ? (int?)LastRevisionId : null,
                                bot = isBot,
                                summary = summary,
                                language = langGroup.Key,
                                add = addExpr.Length == 0 ? null : addExpr,
                                remove = removeExpr.Length == 0 ? null : removeExpr,
                            }), cancellationToken);
                            LoadEntityMinimal(jresult["entity"]);
                            if (!strict) checkbaseRev = false;
                        }
                        break;
                    }
                case nameof(SiteLinks):
                    {
                        var entries = prop.GroupBy(t => ((EntitySiteLink)t.Value!).Site);
                        foreach (var siteGroup in entries)
                        {
                            string? link = null, badges = null;
                            try
                            {
                                var item = siteGroup.Single();
                                if (item.State == EntityEditEntryState.Updated)
                                {
                                    var value = (EntitySiteLink)item.Value!;
                                    link = value.Title;
                                    badges = MediaWikiHelper.JoinValues(value.Badges);
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                throw new ArgumentException("One site can own at most one site link.", nameof(edits));
                            }
                            var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                            {
                                action = "wbsetsitelink",
                                token = WikiSiteToken.Edit,
                                id = Id,
                                @new = Id == null ? FormatEntityType(Type) : null,
                                baserevid = checkbaseRev && LastRevisionId > 0 ? (int?)LastRevisionId : null,
                                bot = isBot,
                                summary = summary,
                                linksite = siteGroup.Key,
                                linktitle = link,
                                badges = badges,
                            }), cancellationToken);
                            LoadEntityMinimal(jresult["entity"]);
                            if (!strict) checkbaseRev = false;
                        }
                        break;
                    }
                case nameof(Claims):
                    foreach (var entry in prop.Where(e => e.State == EntityEditEntryState.Updated))
                    {
                        var value = (Claim)entry.Value!;
                        var claimContract = value.ToContract(false);
                        if (value.Id == null)
                        {
                            // New claim. We need to assign an ID manually.
                            // https://phabricator.wikimedia.org/T182573#3828344
                            if (Id == null)
                            {
                                // This is a new entity, so we need to create it first.
                                var jresult1 = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                                {
                                    action = "wbeditentity",
                                    token = WikiSiteToken.Edit,
                                    @new = FormatEntityType(Type),
                                    bot = isBot,
                                    summary = (string?)null,
                                    data = "{}"
                                }), cancellationToken);
                                if (!strict) checkbaseRev = false;
                                LoadEntityMinimal(jresult1["entity"]);
                            }
                            Debug.Assert(Id != null, "Id is expected to be loaded after entity creation.");
                            claimContract.Id = Utility.NewClaimGuid(Id);
                        }
                        var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                        {
                            action = "wbsetclaim",
                            token = WikiSiteToken.Edit,
                            @new = Id == null ? FormatEntityType(Type) : null,
                            baserevid = checkbaseRev && LastRevisionId > 0 ? (int?)LastRevisionId : null,
                            bot = isBot,
                            summary = summary,
                            claim = Utility.WikiJsonSerializer.Serialize(claimContract),
                        }), cancellationToken);
                        // jresult["claim"] != null
                        LastRevisionId = (int)jresult["pageinfo"]["lastrevid"];
                        if (!strict) checkbaseRev = false;
                    }
                    foreach (var batch in prop.Where(e => e.State == EntityEditEntryState.Removed)
                                 .Select(e => ((Claim)e.Value!).Id).Partition(50))
                    {
                        var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                        {
                            action = "wbremoveclaims",
                            token = WikiSiteToken.Edit,
                            id = Id,
                            @new = Id == null ? FormatEntityType(Type) : null,
                            baserevid = checkbaseRev && LastRevisionId > 0 ? (int?)LastRevisionId : null,
                            bot = isBot,
                            summary = summary,
                            claim = MediaWikiHelper.JoinValues(batch),
                        }), cancellationToken);
                        LastRevisionId = (int)jresult["pageinfo"]["lastrevid"];
                        if (!strict) checkbaseRev = false;
                    }
                    break;
                default:
                    throw new ArgumentException($"Unrecognized {nameof(Entity)} property name: {prop.Key}.");
            }
        }

        void LoadEntityMinimal(JToken jentity)
        {
            Debug.Assert(jentity != null);
            Id = (string)jentity["id"];
            Type = SerializableEntity.ParseEntityType((string)jentity["type"]);
            LastRevisionId = (int)jentity["lastrevid"];
        }
    }
}

/// <summary>
/// Provides options for editing a Wikibase entity.
/// </summary>
/// <remarks>
/// <para>Progressive edit makes one change per MediaWiki API request, and will leave
/// more detailed edit summary on the Wiki. If one of the requests fails,
/// previously made changes will still be kept anyway.</para>
/// <para>By default, if you are editing an existing item, with less than 5 items of changes,
/// WikiClientLibrary will choose progressive edit rather than bulk one.</para>
/// </remarks>
[Flags]
public enum EntityEditOptions
{
    /// <summary>No special options.</summary>
    None = 0,
    /// <summary>Mark the edit as bot edit.</summary>
    Bot = 1,
    /// <summary>Forces progressive edit, even if WCL is creating a new item.
    /// This option cannot be used with <see cref="Bulk"/> and <see cref="ClearData"/>.</summary>
    /// <remarks>Note that setting <see cref="Entity.DataType"/> is not possible in progressive mode,
    /// nor can you change the data type of an existing property due to the limitation of Wikibase.</remarks>
    Progressive = 2,
    /// <summary>Forces bulk edit, even if WCL is editing an existing item with less than 5 items of changes.</summary>
    Bulk = 4,
    /// <summary>Clears all the existing data of the entity before making the changes.
    /// This option implies <see cref="Bulk"/> flag.</summary>
    ClearData = 8 | Bulk,
    /// <summary>When performing progressive edit, check for edit conflicts on every MediaWiki API request,
    /// instead of only checking for conflict before sending the first API request.</summary>
    /// <remarks>When this flag is set, if other user edited the same entity as the one performing progressive edit,
    /// an <see cref="OperationConflictException"/> will be thrown.</remarks>
    StrictEditConflictDetection
}