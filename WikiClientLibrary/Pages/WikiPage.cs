using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Files;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages.Parsing;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Represents a page on MediaWiki site.
    /// </summary>
    public partial class WikiPage
    {

        /// <inheritdoc cref="WikiPage(WikiSite,string,int)"/>
        public WikiPage(WikiSite site, string title) : this(site, title, BuiltInNamespaces.Main)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="WikiPage"/> from page title.
        /// </summary>
        /// <param name="site">The wiki site this page is on.</param>
        /// <param name="title">Page title with or without namespace prefix.</param>
        /// <param name="defaultNamespaceId">The default namespace ID for page title without namespace prefix.</param>
        /// <remarks>The initialized instance does not contain any live information from MediaWiki site.
        /// Use <see cref="RefreshAsync()"/> to fetch for information from server.</remarks>
        public WikiPage(WikiSite site, string title, int defaultNamespaceId)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));
            Site = site;
            var parsedTitle = WikiLink.Parse(site, title, defaultNamespaceId);
            PageStub = new WikiPageStub(parsedTitle.FullTitle, parsedTitle.Namespace!.Id);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="WikiPage"/> from page ID.
        /// </summary>
        /// <param name="site">The wiki site this page is on.</param>
        /// <param name="id">Page ID.</param>
        /// <remarks>The initialized instance does not contain any live information from MediaWiki site.
        /// Use <see cref="RefreshAsync()"/> to fetch for information from server.</remarks>
        public WikiPage(WikiSite site, int id)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
            PageStub = new WikiPageStub(id);
        }

        /// <summary>
        /// Gets the Site the page is on.
        /// </summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Id of the page.
        /// </summary>
        public int Id => PageStub.Id;

        /// <summary>
        /// Namespace id of the page.
        /// </summary>
        public int NamespaceId => PageStub.NamespaceId;

        private List<IWikiPagePropertyGroup>? propertyGroups;
        private IReadOnlyCollection<IWikiPagePropertyGroup>? readonlyPropertyGroups;
        private PageInfoPropertyGroup? pageInfo;

        /// <summary>
        /// Gets the property group of specified type that is attached to this page.
        /// </summary>
        /// <param name="propertyGroupType">Type of the desired property group. Must be a type implementing <see cref="IWikiPagePropertyGroup"/>.</param>
        /// <returns>The property group instance of the specified type, or <c>null</c> if no such item can be found.</returns>
        public IWikiPagePropertyGroup? GetPropertyGroup(Type propertyGroupType)
        {
            var ti = propertyGroupType.GetTypeInfo();
            if (!typeof(IWikiPagePropertyGroup).IsAssignableFrom(ti))
                throw new ArgumentException("propertyGroupType is not a subtype of IWikiPagePropertyGroup.", nameof(propertyGroupType));
            if (propertyGroups == null) return null;
            foreach (var prop in propertyGroups)
            {
                if (ti.IsAssignableFrom(prop.GetType().GetTypeInfo())) return prop;
            }
            return null;
        }

        /// <summary>
        /// Gets the property group of specified type that is attached to this page.
        /// </summary>
        /// <returns>The property group instance of the specified type, or <c>default(T)</c> if no such item can be found.</returns>
        /// <typeparam name="T">The type of the desired property group.</typeparam>
        [return: MaybeNull]
        public T GetPropertyGroup<T>() where T : IWikiPagePropertyGroup
        {
            if (propertyGroups == null) return default;
            foreach (var prop in propertyGroups)
            {
                if (prop is T t) return t;
            }
            return default;
        }

        /// <summary>
        /// Gets a read-only view of all the fetched property groups attached to this page.
        /// </summary>
        public IReadOnlyCollection<IWikiPagePropertyGroup> PropertyGroups
        {
            get
            {
                if (readonlyPropertyGroups != null) return readonlyPropertyGroups;
                if (propertyGroups == null) return Array.Empty<IWikiPagePropertyGroup>();
                var s = new ReadOnlyCollection<IWikiPagePropertyGroup>(propertyGroups);
                Volatile.Write(ref readonlyPropertyGroups, s);
                return s;
            }
        }

        /// <summary>
        /// Gets the id of last revision. In some cases, this property
        /// has non-zero value while <see cref="LastRevision"/> is <c>null</c>.
        /// See <see cref="EditAsync"/> for more information.
        /// </summary>
        /// <seealso cref="Revision.Id"/>
        public int LastRevisionId { get; private set; }

        /// <summary>
        /// Content length, in bytes.
        /// </summary>
        /// <remarks>
        /// Even if you haven't fetched content of the page when calling <see cref="RefreshAsync()"/>,
        /// this property will still get its value.
        /// </remarks>
        /// <seealso cref="Revision.ContentLength"/>
        public int ContentLength => pageInfo?.ContentLength ?? 0;

        /// <summary>
        /// Page touched timestamp. It can be later than the timestamp of the last revision.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Manual:Page_table#page_touched .</remarks>
        public DateTime LastTouched => pageInfo?.LastTouched ?? DateTime.MinValue;

        public IReadOnlyCollection<ProtectionInfo> Protections => pageInfo == null ? Array.Empty<ProtectionInfo>() : pageInfo.Protections;

        /// <summary>
        /// Applicable protection types. (MediaWiki 1.25)
        /// </summary>
        public IReadOnlyCollection<string> RestrictionTypes => pageInfo == null ? Array.Empty<string>() : pageInfo.RestrictionTypes;

        /// <summary>
        /// Gets whether the page exists.
        /// For category, gets whether the categories description page exists.
        /// </summary>
        /// <remarks>
        /// For images existing on Wikimedia Commons, this property will return <c>false</c>,
        /// because they doesn't exist on this site.
        /// </remarks>
        public bool Exists => !PageStub.IsMissing;

        /// <summary>
        /// Gets whether the page is a Special page.
        /// </summary>
        public bool IsSpecialPage => PageStub.IsSpecial;

        /// <summary>
        /// Content model. (MediaWiki 1.22)
        /// </summary>
        /// <remarks>See <see cref="ContentModels"/> for a list of commonly-used content model names.</remarks>
        /// <seealso cref="Revision.ContentModel"/>
        public string? ContentModel { get; private set; }

        /// <summary>
        /// Page language. (MediaWiki 1.24)
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:PageLanguage .</remarks>
        public string? PageLanguage => pageInfo?.PageLanguage;

        /// <summary>
        /// Gets the normalized full title of the page.
        /// </summary>
        /// <remarks>
        /// Normalized title is a title with underscores(_) replaced by spaces,
        /// and the first letter is usually upper-case.
        /// </remarks>
        public string? Title => PageStub.HasTitle ? PageStub.Title : null;

        /// <summary>
        /// Gets the content of the page.
        /// </summary>
        /// <remarks>
        /// <para>This is equivalent to <see cref="LastRevision"/>?.<seealso cref="Content"/>.
        /// To fetch page content, invoke <seealso cref="RefreshAsync()"/> either with <see cref="PageQueryOptions.FetchContent"/> flag
        /// or with <seealso cref="RevisionsPropertyProvider.FetchContent"/> set to <c>true</c>.</para>
        /// <para>To make edit to a page, use <see cref="EditAsync(WikiPageEditOptions)"/>
        /// or <see cref="EditSectionAsync"/>. As a side effect, these functions will
        /// reset <seealso cref="Content"/> into <c>null</c>. You will need to refresh <seealso cref="WikiPage"/>
        /// once more if you want to explicitly re-retrieve the content from MediaWiki server.</para>
        /// </remarks>
        public string? Content => LastRevision?.Content;

        /// <summary>
        /// Gets the latest revision of the page.
        /// </summary>
        /// <remarks>Make sure to invoke <see cref="RefreshAsync()"/> before getting the value.</remarks>
        public Revision? LastRevision { get; private set; }

        /// <summary>
        /// Gets the latest file revision information, if applicable.
        /// </summary>
        /// <remarks>Make sure to invoke <see cref="RefreshAsync()"/> before getting the value.</remarks>
        public FileRevision? LastFileRevision { get; private set; }

        /// <summary>
        /// Determines whether the existing page is a disambiguation page.
        /// </summary>
        /// <exception cref="InvalidOperationException">The page does not exist.</exception>
        /// <remarks>
        /// It's recommended to use this method instead of accessing
        /// <see cref="PagePropertyCollection.Disambiguation"/> directly. Because the latter
        /// property is available only if there's Disambiguator extension installed
        /// on the MediaWiki site.
        /// </remarks>
        public async Task<bool> IsDisambiguationAsync()
        {
            var title = RequiresTitle();
            AssertExists();
            // If the Disambiguator extension is loaded, use it
            if (Site.Extensions.Contains("Disambiguator"))
            {
                var group = GetPropertyGroup<PagePropertiesPropertyGroup>();
                if (group != null)
                    return group.PageProperties.Disambiguation;
            }
            // Check whether the page has transcluded one of the DAB templates.
            var dabt = await Site.DisambiguationTemplatesAsync;
            var dabp = await RequestHelper.EnumTransclusionsAsync(Site, title,
                new[] { BuiltInNamespaces.Template }, dabt, 1).AnyAsync();
            return dabp;
        }

#region Redirect

        /// <summary>
        /// Determines the last version of the page is a redirect page.
        /// </summary>
        public bool IsRedirect => pageInfo?.IsRedirect ?? false;

        /// <summary>
        /// Gets a list indicating the titles passed (except the final destination) when trying to
        /// resolve the redirects in the last <see cref="RefreshAsync()"/> invocation.
        /// </summary>
        /// <value>
        /// A sequence of strings indicating the titles passed when trying to
        /// resolve the redirects in the last <see cref="RefreshAsync()"/> invocation.
        /// OR an empty sequence if there's no redirect resolved, or redirect resolution
        /// has been disabled.
        /// </value>
        public IList<string> RedirectPath { get; internal set; } = Array.Empty<string>();

        /// <summary>
        /// If current page is a redirect, tries to get the final target of the redirect.
        /// </summary>
        /// <returns>
        /// A <see cref="WikiPage"/> of the target.
        /// OR <c>null</c> if the page is not a redirect page.
        /// </returns>
        /// <remarks>
        /// The method will create a new <see cref="WikiPage"/> instance with the
        /// same <see cref="Title"/> of current instance, and invoke 
        /// <c>Page.RefreshAsync(PageQueryOptions.ResolveRedirects)</c>
        /// to resolve the redirects.
        /// </remarks>
        public async Task<WikiPage?> GetRedirectTargetAsync()
        {
            var newPage = new WikiPage(Site, RequiresTitle());
            await newPage.RefreshAsync(PageQueryOptions.ResolveRedirects);
            if (newPage.RedirectPath.Count > 0) return newPage;
            return null;
        }

#endregion

#region Query

        [MemberNotNull(nameof(Title))]
        protected internal string RequiresTitle()
        {
            if (string.IsNullOrEmpty(Title))
                throw new InvalidOperationException("Page title is not available. Use RefreshAsync to fetch it.");
            return Title;
        }

        protected void AssertExists()
        {
            if (!Exists) throw new InvalidOperationException(string.Format(Prompts.ExceptionWikiPageNotExists1, this));
        }

        protected internal virtual void OnLoadPageInfo(JObject jpage, IWikiPageQueryProvider options)
        {
            // Initialize
            propertyGroups?.Clear();
            // Update page stub
            PageStub = MediaWikiHelper.PageStubFromJson(jpage);
            // Load page info
            // Invalid page title (like File:)
            if (PageStub.IsInvalid)
            {
                return;
            }
            // Load property groups
            foreach (var group in options.ParsePropertyGroups(jpage))
            {
                Debug.Assert(group != null, "The returned sequence from IWikiPageQueryParameters.ParsePropertyGroups contains null item.");
                if (propertyGroups == null) propertyGroups = new List<IWikiPagePropertyGroup>();
                propertyGroups.Add(group);
            }
            // Check if the client has requested for revision content…
            LastRevision = GetPropertyGroup<RevisionsPropertyGroup>()?.LatestRevision;
            LastFileRevision = GetPropertyGroup<FileInfoPropertyGroup>()?.LatestRevision;
            pageInfo = GetPropertyGroup<PageInfoPropertyGroup>();
            LastRevisionId = pageInfo?.LastRevisionId ?? 0;
            ContentModel = pageInfo?.ContentModel;
        }

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryProvider, CancellationToken)"/>
        /// <summary>
        /// Fetch information for the page.
        /// This overload will not fetch content.
        /// </summary>
        /// <remarks>
        /// For fetching multiple pages at one time, see <see cref="WikiPageExtensions.RefreshAsync(IEnumerable{WikiPage})"/>.
        /// </remarks>
        public Task RefreshAsync()
        {
            return RefreshAsync(PageQueryOptions.None, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryProvider, CancellationToken)"/>
        public Task RefreshAsync(IWikiPageQueryProvider options)
        {
            return RefreshAsync(options, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryProvider, CancellationToken)"/>
        public Task RefreshAsync(PageQueryOptions options)
        {
            return RefreshAsync(options, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryProvider, CancellationToken)"/>
        public Task RefreshAsync(PageQueryOptions options, CancellationToken cancellationToken)
        {
            return RefreshAsync(MediaWikiHelper.QueryProviderFromOptions(options), cancellationToken);
        }

        /// <summary>
        /// Fetch information for the page.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <remarks>
        /// For fetching multiple pages at one time, see <see cref="WikiPageExtensions.RefreshAsync(IEnumerable{WikiPage}, PageQueryOptions)"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Circular redirect detected when resolving redirects.</exception>
        public Task RefreshAsync(IWikiPageQueryProvider options, CancellationToken cancellationToken)
        {
            return RequestHelper.RefreshPagesAsync(new[] { this }, options, cancellationToken);
        }

#endregion

#region Modification

        /// <summary>
        /// Edits the main content of the current page, using specified new page content and other options.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <param name="options">page edit options, including new content.</param>
        /// <returns><c>true</c> if page content has been changed; <c>false</c> otherwise.</returns>
        /// <remarks>
        /// This action will refill <see cref="Id" />, <see cref="Title"/>,
        /// <see cref="ContentModel"/>, <see cref="LastRevisionId"/>, and invalidate
        /// <see cref="ContentLength"/>, <see cref="LastRevision"/>, <see cref="Content"/>,
        /// and <see cref="LastTouched"/>.
        /// You should call <see cref="RefreshAsync()"/> again if you're interested in them.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Cannot create actual page in the specified namespace.</exception>
        /// <exception cref="OperationConflictException">Edit conflict detected.</exception>
        /// <exception cref="UnauthorizedOperationException">You have no rights to edit the page.</exception>
        public async Task<bool> EditAsync(WikiPageEditOptions options)
            => await EditAsync(options, null, null);

        /// <inheritdoc cref="EditAsync(WikiPageEditOptions)"/>
        /// <summary>
        /// Edits the specified section of the current page, using specified new section content and other options.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <param name="sectionId">section identifier. <c>"0"</c> for the top section. Often a positive integer, but can also be non-numeric when the section is inside templates (<c>"T-1"</c>).</param>
        /// <seealso cref="ContentSectionInfo.Index"/>
        public async Task<bool> EditSectionAsync(string sectionId, WikiPageEditOptions options)
        {
            if (sectionId == null) throw new ArgumentNullException(nameof(sectionId));
            return await EditAsync(options, sectionId, null);
        }

        /// <inheritdoc cref="EditAsync(WikiPageEditOptions)"/>
        /// <param name="sectionTitle">title for the new section. (MediaWiki 1.19+)</param>
        public async Task<bool> AddSectionAsync(string sectionTitle, WikiPageEditOptions options)
        {
            if (sectionTitle == null) throw new ArgumentNullException(nameof(sectionTitle));
            return await EditAsync(options, "new", sectionTitle);
        }

        private async Task<bool> EditAsync(WikiPageEditOptions options, string? sectionId, string? sectionTitle)
        {
            using (Site.BeginActionScope(this))
            using (await Site.ModificationThrottler.QueueWorkAsync("Edit: " + this, options.CancellationToken))
            {
                // When passing this to the Edit API, always pass the token parameter last
                // (or at least after the text parameter). That way, if the edit gets interrupted,
                // the token won't be passed and the edit will fail.
                // This is done automatically by mw.Api.
                JToken jresult;
                try
                {
                    jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "edit",
                        token = WikiSiteToken.Edit,
                        title = PageStub.HasTitle ? PageStub.Title : null,
                        pageid = PageStub.HasTitle ? null : (int?)PageStub.Id,
                        minor = options.Minor,
                        bot = options.Bot,
                        recreate = true,
                        maxlag = 5,
                        basetimestamp = LastRevision?.TimeStamp,
                        watchlist = options.Watch,
                        summary = options.Summary,
                        text = options.Content,
                        section = sectionId,
                        sectiontitle = sectionTitle,
                    }), options.CancellationToken);
                }
                catch (OperationFailedException ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case "protectedpage":
                            throw new UnauthorizedOperationException(ex);
                        case "unsupportednamespace": // MW 1.19
                        case "pagecannotexist": // newer versions
                            throw new InvalidOperationException(ex.ErrorMessage, ex);
                        default:
                            throw;
                    }
                }
                var jedit = jresult["edit"];
                var result = (string)jedit["result"];
                if (result == "Success")
                {
                    if (jedit["nochange"] != null)
                    {
                        Site.Logger.LogInformation("Submitted empty edit to page.");
                        return false;
                    }
                    ContentModel = (string)jedit["contentmodel"];
                    LastRevisionId = (int)jedit["newrevid"];
                    LastRevision = null;
                    pageInfo = null;
                    // jedit["ns"] == null
                    PageStub = new WikiPageStub((int)jedit["pageid"], (string)jedit["title"], PageStub.NamespaceId);
                    Site.Logger.LogInformation("Edited page. New revid={RevisionId}.", LastRevisionId);
                    return true;
                }
                // No "errors" in json result but result is not Success.
                throw new OperationFailedException(result, (string?)null);
            }
        }

#endregion

#region Management

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public Task MoveAsync(string newTitle, string reason, PageMovingOptions options)
        {
            return MoveAsync(newTitle, reason, options, AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public Task MoveAsync(string newTitle, string reason)
        {
            return MoveAsync(newTitle, reason, PageMovingOptions.None, AutoWatchBehavior.Default,
                CancellationToken.None);
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public Task MoveAsync(string newTitle)
        {
            return MoveAsync(newTitle, null, PageMovingOptions.None, AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public async Task MoveAsync(string newTitle, string? reason, PageMovingOptions options, AutoWatchBehavior watch,
            CancellationToken cancellationToken)
        {
            if (newTitle == null) throw new ArgumentNullException(nameof(newTitle));
            if (newTitle == Title) return;
            using (Site.BeginActionScope(this))
            using (await Site.ModificationThrottler.QueueWorkAsync("Move: " + this, cancellationToken))
            {
                // When passing this to the Edit API, always pass the token parameter last
                // (or at least after the text parameter). That way, if the edit gets interrupted,
                // the token won't be passed and the edit will fail.
                // This is done automatically by mw.Api.
                JToken jresult;
                try
                {
                    jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "move",
                        token = WikiSiteToken.Move,
                        from = PageStub.HasTitle ? PageStub.Title : null,
                        fromid = PageStub.HasTitle ? null : (int?)PageStub.Id,
                        to = newTitle,
                        maxlag = 5,
                        movetalk = (options & PageMovingOptions.LeaveTalk) != PageMovingOptions.LeaveTalk,
                        movesubpages = (options & PageMovingOptions.MoveSubpages) == PageMovingOptions.MoveSubpages,
                        noredirect = (options & PageMovingOptions.NoRedirect) == PageMovingOptions.NoRedirect,
                        ignorewarnings = (options & PageMovingOptions.IgnoreWarnings) ==
                                         PageMovingOptions.IgnoreWarnings,
                        watchlist = watch,
                        reason = reason,
                    }), cancellationToken);
                }
                catch (OperationFailedException ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case "cantmove":
                        case "protectedpage":
                        case "protectedtitle":
                            throw new UnauthorizedOperationException(ex);
                        default:
                            if (ex.ErrorCode != null && ex.ErrorCode.StartsWith("cantmove"))
                                throw new UnauthorizedOperationException(ex);
                            throw;
                    }
                }
                var fromTitle = (string)jresult["move"]["from"];
                var toTitle = (string)jresult["move"]["to"];
                Site.Logger.LogInformation("Page [[{fromTitle}]] has been moved to [[{toTitle}]].", fromTitle, toTitle);
                var link = WikiLink.Parse(Site, toTitle);
                Debug.Assert(link.Namespace != null);
                PageStub = new WikiPageStub(PageStub.Id, toTitle, link.Namespace.Id);
            }
        }

        /// <summary>
        /// Deletes the current page.
        /// </summary>
        public Task<bool> DeleteAsync(string reason)
        {
            return DeleteAsync(reason, AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <summary>
        /// Deletes the current page.
        /// </summary>
        public Task<bool> DeleteAsync(string reason, AutoWatchBehavior watch)
        {
            return DeleteAsync(reason, watch, CancellationToken.None);
        }

        /// <summary>
        /// Deletes the current page.
        /// </summary>
        public async Task<bool> DeleteAsync(string reason, AutoWatchBehavior watch, CancellationToken cancellationToken)
        {
            using (Site.BeginActionScope(this))
            using (await Site.ModificationThrottler.QueueWorkAsync("Delete: " + this, cancellationToken))
            {
                JToken jresult;
                try
                {
                    jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "delete",
                        token = WikiSiteToken.Delete,
                        title = PageStub.HasTitle ? PageStub.Title : null,
                        pageid = PageStub.HasTitle ? null : (int?)PageStub.Id,
                        maxlag = 5,
                        watchlist = watch,
                        reason = reason,
                    }), cancellationToken);
                }
                catch (OperationFailedException ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case "cantdelete": // Couldn't delete "title". Maybe it was deleted already by someone else
                        case "missingtitle":
                            return false;
                        case "permissiondenied":
                            throw new UnauthorizedOperationException(ex);
                    }
                    throw;
                }
                var title = (string)jresult["delete"]["title"];
                PageStub = new WikiPageStub(WikiPageStub.MissingPageIdMask, PageStub.Title, PageStub.NamespaceId);
                LastRevision = null;
                LastRevisionId = 0;
                Site.Logger.LogInformation("[[{Page}]] has been deleted.", title);
                return true;
            }
        }

        /// <summary>
        /// Asynchronously purges the current page.
        /// </summary>
        /// <returns><c>true</c> if the page has been successfully purged.</returns>
        public Task<bool> PurgeAsync()
        {
            return PurgeAsync(PagePurgeOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously purges the current page with the given options.
        /// </summary>
        /// <returns><c>true</c> if the page has been successfully purged.</returns>
        public Task<bool> PurgeAsync(PagePurgeOptions options)
        {
            return PurgeAsync(options, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously purges the current page with the given options.
        /// </summary>
        /// <returns><c>true</c> if the page has been successfully purged.</returns>
        public async Task<bool> PurgeAsync(PagePurgeOptions options, CancellationToken cancellationToken)
        {
            var failure = await RequestHelper.PurgePagesAsync(new[] { this }, options, cancellationToken);
            return failure.Count == 0;
        }

#endregion

        /// <summary>
        /// Gets an initialized <see cref="WikiPageStub"/> from the current instance.
        /// </summary>
        public WikiPageStub PageStub { get; private set; }

        /// <summary>
        /// Implicitly converts the WikiPage instance into PageStub.
        /// </summary>
        /// <param name="page">source WikiPage; can be <c>null</c>.</param>
        public static implicit operator WikiPageStub(WikiPage? page)
        {
            if (page == null) return WikiPageStub.Empty;
            return page.PageStub;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Title) ? ("#" + Id) : Title;
        }

    }

    /// <summary>
    /// Specifies whether to watch the page after editing it.
    /// </summary>
    public enum AutoWatchBehavior
    {
        /// <summary>
        /// Use the preference settings. (watchlist=preferences)
        /// </summary>
        Default = 0,

        /// <summary>
        /// Do not change watchlist.
        /// </summary>
        None = 1,
        Watch = 2,
        Unwatch = 3,
    }

    /// <summary>
    /// Specifies options for moving pages.
    /// </summary>
    [Flags]
    public enum PageMovingOptions
    {
        None = 0,

        /// <summary>
        /// Do not attempt to move talk pages.
        /// </summary>
        LeaveTalk = 1,

        /// <summary>
        /// Move subpages, if applicable.
        /// </summary>
        /// <remarks>This is usually not recommended because you still cannot overwrite existing subpages.</remarks>
        MoveSubpages = 2,

        /// <summary>
        /// Don't create a redirect. Requires the suppressredirect right,
        /// which by default is granted only to bots and sysops
        /// </summary>
        NoRedirect = 4,

        /// <summary>
        /// Ignore any warnings.
        /// </summary>
        IgnoreWarnings = 8,
    }

    /// <summary>
    /// Options for refreshing a <see cref="WikiPage"/> object.
    /// For more accurate control on what information to fetch from server when calling library functions,
    /// consider using overloads that accept <see cref="IWikiPageQueryProvider"/>.
    /// </summary>
    /// <seealso cref="WikiPage.RefreshAsync(PageQueryOptions)"/>
    /// <seealso cref="WikiPageGenerator{TItem}.EnumPagesAsync(PageQueryOptions)"/>
    /// <seealso cref="WikiPageQueryProvider.FromOptions"/>
    /// <seealso cref="MediaWikiHelper.QueryProviderFromOptions"/>
    [Flags]
    public enum PageQueryOptions
    {
        /// <summary>
        /// Fetch basic page information using the following property providers:
        /// <list type="bullet">
        /// <item><term><see cref="PageInfoPropertyProvider"/></term></item>
        /// <item><term><see cref="RevisionsPropertyProvider"/> (<see cref="RevisionsPropertyProvider.FetchContent"/> is <c>false</c>.)</term></item>
        /// <item><term><see cref="CategoryInfoPropertyProvider"/></term></item>
        /// <item><term><see cref="FileInfoPropertyProvider"/></term></item>
        /// <item><term><see cref="PagePropertiesPropertyProvider"/></term></item>
        /// </list>
        /// </summary>
        None = 0,

        /// <summary>
        /// Also fetch content of the page. (<see cref="RevisionsPropertyProvider.FetchContent"/> is <c>false</c>.)
        /// </summary>
        FetchContent = 1,

        /// <summary>
        /// Resolves directs automatically. This may later change <see cref="WikiPage.Title"/>.
        /// This option cannot be used with generators.
        /// In the case of multiple redirects (A→B→C→…→X), all the redirects on the path will be resolved.
        /// This will set <see cref="WikiPageQueryProvider.ResolveRedirects"/> to <c>true</c>.
        /// </summary>
        ResolveRedirects = 2,
    }

    /// <summary>
    /// Options for purging a page.
    /// </summary>
    [Flags]
    public enum PagePurgeOptions
    {
        None = 0,

        /// <summary>
        /// Updates the link tables.
        /// </summary>
        ForceLinkUpdate = 1,

        /// <summary>
        /// Like <see cref="ForceLinkUpdate"/>, but also do <see cref="ForceLinkUpdate"/> on
        /// any page that transcludes the current page. This is akin to making an edit to
        /// a template. Note that the job queue is used for this operation, so there may
        /// be a slight delay when doing this for pages used a large number of times.
        /// </summary>
        ForceRecursiveLinkUpdate = 2,
    }

    /// <summary>
    /// Page protection information.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ProtectionInfo
    {

        [JsonProperty]
        public string Type { get; private set; } = "";

        [JsonProperty]
        public string Level { get; private set; } = "";

        [JsonProperty("expiry")]
        public DateTime Expiry { get; private set; }

        [JsonProperty]
        public bool Cascade { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}, {Level}, {Expiry}, {(Cascade ? "Cascade" : "")}";
        }
    }

    /// <summary>
    /// A read-only collection Containing additional page properties.
    /// </summary>
    [JsonDictionary]
    public class PagePropertyCollection : WikiReadOnlyDictionary
    {

        /// <summary>
        /// An empty instance.
        /// </summary>
        internal static readonly PagePropertyCollection Empty = new PagePropertyCollection();

        static PagePropertyCollection()
        {
            Empty.MakeReadonly();
        }

        /// <summary>
        /// Determines whether the page is a disambiguation page.
        /// This is raw value and only works when Extension:Disambiguator presents.
        /// Please use <see cref="WikiPage.IsDisambiguationAsync"/> instead.
        /// </summary>
        public bool Disambiguation => GetBooleanValue("disambiguation");

        public string? DisplayTitle => GetStringValue("displaytitle");

        public string? PageImage => GetStringValue("page_image");

        public bool IsHiddenCategory => GetBooleanValue("hiddencat");

    }

#if BCL_FEATURE_RECORD
    public record WikiPageEditOptions
#else
    public class WikiPageEditOptions
#endif
    {

        /// <summary>
        /// The new page content; use empty string (<c>""</c>) to clear the page content.
        /// </summary>
        /// <remarks>
        /// <para>When using with <see cref="WikiPage.EditSectionAsync"/>, the content should also include
        /// the section heading part (<c>== Title ==</c>). Otherwise, the section heading will be removed,
        /// and the section will be merged with the previous one.</para>
        /// <para>When using with <see cref="WikiPage.AddSectionAsync"/>, the content does not include
        /// the section heading part.</para>
        /// </remarks>
        public string Content
        {
#if BCL_FEATURE_RECORD
            get; init;
#else
            get; set;
#endif
        } = "";

        /// <summary>
        /// The edit summary. Leave it as <c>null</c> or empty string (<c>""</c>) to use the default edit summary.
        /// </summary>
        public string? Summary
        {
#if BCL_FEATURE_RECORD
            get; init;
#else
            get; set;
#endif
        }

        /// <summary>
        /// Whether the edit is a minor edit. (See <a href="https://meta.wikimedia.org/wiki/Help:Minor_edit">m:Help:Minor Edit</a>)
        /// </summary>
        public bool Minor
        {
#if BCL_FEATURE_RECORD
            get; init;
#else
            get; set;
#endif
        }

        /// <summary>
        /// Whether to mark the edit as bot; even if you are using a bot account the edits will not be marked unless you set this flag.
        /// </summary>
        public bool Bot
        {
#if BCL_FEATURE_RECORD
            get; init;
#else
            get; set;
#endif
        }

        /// <summary>
        /// Specify how the watchlist is affected by this edit.
        /// </summary>
        public AutoWatchBehavior Watch
        {
#if BCL_FEATURE_RECORD
            get; init;
#else
            get; set;
#endif
        }

        /// <summary>
        /// A token used to cancel the operation.
        /// </summary>
        public CancellationToken CancellationToken
        {
#if BCL_FEATURE_RECORD
            get; init;
#else
            get; set;
#endif
        }

    }
}