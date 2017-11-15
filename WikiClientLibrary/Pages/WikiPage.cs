using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Represents a page on MediaWiki site.
    /// </summary>
    public partial class WikiPage
    {
        
        public WikiPage(WikiSite site, string title) : this(site, title, BuiltInNamespaces.Main)
        {
        }

        public WikiPage(WikiSite site, string title, int defaultNamespaceId)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));
            Site = site;
            var parsedTitle = WikiLink.Parse(site, title, defaultNamespaceId);
            Title = parsedTitle.FullTitle;
            NamespaceId = parsedTitle.Namespace.Id;
        }

        internal WikiPage(WikiSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
            Site = site;
        }

        /// <summary>
        /// Gets the Site the page is on.
        /// </summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Id of the page.
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Namespace id of the page.
        /// </summary>
        public int NamespaceId { get; private set; }

        /// <summary>
        /// Gets the id of last revision. In some cases, this property
        /// has non-zero value while <see cref="LastRevision"/> is <c>null</c>.
        /// See <see cref="UpdateContentAsync(string)"/> for more information.
        /// </summary>
        public int LastRevisionId { get; private set; }

        /// <summary>
        /// Content length, in bytes.
        /// </summary>
        /// <remarks>
        /// Even if you haven't fetched content of the page when calling <see cref="RefreshAsync()"/>,
        /// this property will still get its value.
        /// </remarks>
        public int ContentLength { get; private set; }

        /// <summary>
        /// Page touched timestamp. It can be later than the timestamp of the last revision.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Manual:Page_table#page_touched .</remarks>
        public DateTime LastTouched { get; private set; }

        public IReadOnlyCollection<ProtectionInfo> Protections { get; private set; }

        /// <summary>
        /// Applicable protection types. (MediaWiki 1.25)
        /// </summary>
        public IReadOnlyCollection<string> RestrictionTypes { get; private set; }

        /// <summary>
        /// Gets whether the page exists.
        /// For category, gets whether the categories description page exists.
        /// </summary>
        /// <remarks>
        /// For images existing on Wikimedia Commons, this property will return <c>false</c>,
        /// because they doesn't exist on this site.
        /// </remarks>
        public bool Exists { get; private set; }

        /// <summary>
        /// Gets whether the page is a Special page.
        /// </summary>
        public bool IsSpecialPage { get; private set; }

        /// <summary>
        /// Content model. (MediaWiki 1.22)
        /// </summary>
        /// <remarks>See <see cref="ContentModels"/> for a list of commonly-used content model names.</remarks>
        public string ContentModel { get; private set; }

        /// <summary>
        /// Page language. (MediaWiki 1.24)
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:PageLanguage .</remarks>
        public string PageLanguage { get; private set; }

        /// <summary>
        /// Gets the normalized full title of the page.
        /// </summary>
        /// <remarks>
        /// Normalized title is a title with underscores(_) replaced by spaces,
        /// and the first letter is usually upper-case.
        /// </remarks>
        public string Title { get; protected set; }

        /// <summary>
        /// Gets / Sets the content of the page.
        /// </summary>
        /// <remarks>
        /// <para>To make the edit to a page, after setting this property,
        /// call <see cref="UpdateContentAsync(string)"/> to submit the change.</para>
        /// <para>To fetch for the value of this property,
        /// specify <see cref="PageQueryOptions.FetchContent"/>
        /// when calling <see cref="RefreshAsync(PageQueryOptions)"/>.</para>
        /// </remarks>
        public string Content { get; set; }

        /// <summary>
        /// Gets the latest revision of the page.
        /// </summary>
        /// <remarks>Make sure to invoke <see cref="RefreshAsync()"/> before getting the value.</remarks>
        public Revision LastRevision { get; private set; }

        /// <summary>
        /// Gets the plain-text extract of the page content.
        /// </summary>
        /// <remarks>To fetch for the value of this property,
        /// specify <see cref="PageQueryOptions.FetchExtract"/>
        /// when calling <see cref="RefreshAsync(PageQueryOptions)"/>.
        /// </remarks>
        public string Extract { get; private set; }

        /// <summary>
        /// Gets the primary geo-coordinate associated with the page.
        /// </summary>
        /// <remarks>To fetch for the value of this property,
        /// specify <see cref="PageQueryOptions.FetchGeoCoordinate"/>
        /// when calling <see cref="RefreshAsync(PageQueryOptions)"/>.
        /// </remarks>
        public GeoCoordinate PrimaryCoordinate { get; private set; }

        /// <summary>
        /// Gets the properties of the page.
        /// </summary>
        public PagePropertyCollection PageProperties { get; private set; }

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
            AssertExists();
            // If the Disambiguator extension is loaded, use it
            if (Site.Extensions.Contains("Disambiguator"))
                return PageProperties.Disambiguation;
            // Check whether the page has transcluded one of the DAB templates.
            var dabt = await Site.DisambiguationTemplatesAsync;
            var dabp = await RequestHelper.EnumTransclusionsAsync(Site, Title,
                new[] {BuiltInNamespaces.Template}, dabt, 1).Any();
            return dabp;
        }

        #region Redirect

        /// <summary>
        /// Determines the last version of the page is a redirect page.
        /// </summary>
        public bool IsRedirect { get; private set; }

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
        public IList<string> RedirectPath { get; internal set; } = EmptyStrings;

        /// <summary>
        /// Tries to get the final target of the redirect page.
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
        public async Task<WikiPage> GetRedirectTargetAsync()
        {
            var newPage = new WikiPage(Site, Title);
            await newPage.RefreshAsync(PageQueryOptions.ResolveRedirects);
            if (newPage.RedirectPath.Count > 0) return newPage;
            return null;
        }

        #endregion

        private static bool AreIdEquals(int id1, int id2)
        {
            if (id1 == id2) return false;
            // For inexistent pages, id is negative.
            if (id2 > 0 && id1 > 0 || Math.Sign(id2) != Math.Sign(id1)) return true;
            return false;
        }

        #region Query

        private static readonly WikiPage[] EmptyPages = new WikiPage[0];

        private static readonly string[] EmptyStrings = new string[0];

        protected void AssertExists()
        {
            if (!Exists) throw new InvalidOperationException($"The page {this} does not exist.");
        }

        /// <summary>
        /// Loads page information from JSON.
        /// </summary>
        /// <param name="prop">query.pages.xxx property.</param>
        internal void LoadFromJson(JProperty prop)
        {
            var id = Convert.ToInt32(prop.Name);
            // I'm not sure whether this assertion holds.
            Debug.Assert(id != 0);
            Id = id;
            var page = (JObject) prop.Value;
            OnLoadPageInfo(page);
            // TODO Cache content
            LoadLastRevision(page);
        }

        protected virtual void OnLoadPageInfo(JObject jpage)
        {
            Title = (string) jpage["title"];
            // Invalid page title (like Special:)
            if (jpage["invalid"] != null)
            {
                var reason = (string) jpage["invalidreason"];
                throw new OperationFailedException(reason);
            }
            NamespaceId = (int) jpage["ns"];
            Exists = jpage["missing"] == null;
            ContentModel = (string) jpage["contentmodel"];
            PageLanguage = (string) jpage["pagelanguage"];
            IsSpecialPage = jpage["special"] != null;
            IsRedirect = jpage["redirect"] != null;
            Extract = (string)jpage["extract"];
            var jcoordinates = (JArray)jpage["coordinates"];
            if (jcoordinates != null && jcoordinates.HasValues)
            {
                // Prefer primary coordinates
                PrimaryCoordinate = MediaWikiHelper.GeoCoordinateFromJson((JObject)
                (jcoordinates.FirstOrDefault(coord => coord["primary"] != null)
                 ?? jcoordinates.First));
            }
            else
            {
                PrimaryCoordinate = GeoCoordinate.Empty;
            }
            if (Exists && !IsSpecialPage)
            {
                ContentLength = (int) jpage["length"];
                LastRevisionId = (int) jpage["lastrevid"];
                LastTouched = (DateTime) jpage["touched"];
                Protections = jpage["protection"].ToObject<IReadOnlyCollection<ProtectionInfo>>(
                    Utility.WikiJsonSerializer);
                RestrictionTypes = jpage["restrictiontypes"]?.ToObject<IReadOnlyCollection<string>>(
                                       Utility.WikiJsonSerializer) ?? EmptyStrings;
                PageProperties = jpage["pageprops"]?.ToObject<PagePropertyCollection>(Utility.WikiJsonSerializer)
                                 ?? PagePropertyCollection.Empty;
            }
            else
            {
                // N / A
                ContentLength = 0;
                LastRevisionId = 0;
                LastTouched = DateTime.MinValue;
                Protections = null;
                RestrictionTypes = null;
                PageProperties = null;
            }
        }

        /// <summary>
        /// Loads the last revision from JSON, assuming it's the latest revision.
        /// </summary>
        /// <param name="pageInfo">query.pages.xxx property value.</param>
        private void LoadLastRevision(JObject pageInfo)
        {
            var revision = (JObject) pageInfo["revisions"]?.LastOrDefault();
            if (revision != null)
            {
                var serializer = Utility.CreateWikiJsonSerializer();
                LastRevision = revision.ToObject<Revision>(serializer);
                LastRevision.Page = ToPageStub();
                // Check if the client has requested for revision content…
                if (LastRevision.Content != null)
                    Content = LastRevision.Content;
            }
            else
            {
                // No revisions available.
                LastRevision = null;
            }
        }


        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryParameters, CancellationToken)"/>
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

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryParameters, CancellationToken)"/>
        public Task RefreshAsync(IWikiPageQueryParameters options)
        {
            return RefreshAsync(options, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryParameters, CancellationToken)"/>
        public Task RefreshAsync(PageQueryOptions options)
        {
            return RefreshAsync(options, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IWikiPageQueryParameters, CancellationToken)"/>
        public Task RefreshAsync(PageQueryOptions options, CancellationToken cancellationToken)
        {
            return RefreshAsync(MediaWikiHelper.GetQueryParams(options), cancellationToken);
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
        public Task RefreshAsync(IWikiPageQueryParameters options, CancellationToken cancellationToken)
        {
            return RequestHelper.RefreshPagesAsync(new[] {this}, options, cancellationToken);
        }

        /// <summary>
        /// Enumerates revisions of the page, descending in time, without revision content.
        /// This overload asks for as many items as possible per request. This is usually 500 for user, and 5000 for bots.
        /// </summary>
        /// <remarks>To gain full control of revision enumeration, you can use <see cref="RevisionsGenerator" />.</remarks>
        [Obsolete("Please use RevisionsGenerator class or WikiPageExtensions.CreateRevisionsGenerator extension method instead.")]
        public IAsyncEnumerable<Revision> EnumRevisionsAsync()
        {
            return EnumRevisionsAsync(500);
        }

        /// <summary>
        /// Enumerates revisions of the page, descending in time, without revision content.
        /// </summary>
        /// <param name="pagingSize">Maximum items returned per request.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagingSize"/> is non-positive.</exception>
        /// <remarks>To gain full control of revision enumeration, you can use <see cref="RevisionsGenerator" />.</remarks>
        [Obsolete("Please use RevisionsGenerator class or WikiPageExtensions.CreateRevisionsGenerator extension method instead.")]
        public IAsyncEnumerable<Revision> EnumRevisionsAsync(int pagingSize)
        {
            return EnumRevisionsAsync(pagingSize, PageQueryOptions.None);
        }

        /// <summary>
        /// Enumerates revisions of the page, descending in tim.
        /// </summary>
        /// <param name="pagingSize">Maximum items returned per request.</param>
        /// <param name="options">Options for revision listing. Note <see cref="PageQueryOptions.ResolveRedirects"/> will raise exception.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagingSize"/> is non-positive.</exception>
        /// <remarks>To gain full control of revision enumeration, you can use <see cref="RevisionsGenerator" />.</remarks>
        [Obsolete("Please use RevisionGenerator class or WikiPageExtensions.CreateRevisionGenerator extension method instead.")]
        public IAsyncEnumerable<Revision> EnumRevisionsAsync(int pagingSize, PageQueryOptions options)
        {
            if (pagingSize <= 0) throw new ArgumentOutOfRangeException(nameof(pagingSize));
            var gen = new RevisionsGenerator(Site, Title) { PaginationSize = pagingSize};
            return gen.EnumItemsAsync();
        }

        /// <summary>
        /// Enumerate all links on the pages.
        /// </summary>
        [Obsolete("Please use LinksGenerator class or WikiPageExtensions.CreateLinksGenerator extension method instead.")]
        public IAsyncEnumerable<string> EnumLinksAsync()
        {
            return EnumLinksAsync(null);
        }

        /// <summary>
        /// Enumerate all links on the pages.
        /// </summary>
        /// <param name="namespaces">
        /// Only list links to pages in these namespaces.
        /// If this is empty or <c>null</c>, all the pages will be listed.
        /// </param>
        [Obsolete("Please use LinksGenerator class or WikiPageExtensions.CreateLinksGenerator extension method instead.")]
        public IAsyncEnumerable<string> EnumLinksAsync(IEnumerable<int> namespaces)
        {
            return new LinksGenerator(Site, Title) {NamespaceIds = namespaces}
                .EnumItemsAsync().Select(stub => stub.Title);
        }

        /// <summary>
        /// Enumerate all pages (typically templates) transcluded on the pages.
        /// </summary>
        [Obsolete("Please use TransclusionsGenerator class or WikiPageExtensions.CreateTransclusionsGenerator extension method instead.")]
        public IAsyncEnumerable<string> EnumTransclusionsAsync()
        {
            return EnumTransclusionsAsync(null);
        }

        /// <summary>
        /// Enumerate all pages (typically templates) transcluded on the pages.
        /// </summary>
        /// <param name="namespaces">
        /// Only list links to pages in these namespaces.
        /// If this is empty or <c>null</c>, all the transcluded pages will be listed.
        /// </param>
        [Obsolete("Please use TransclusionsGenerator class or WikiPageExtensions.CreateTransclusionsGenerator extension method instead.")]
        public IAsyncEnumerable<string> EnumTransclusionsAsync(IEnumerable<int> namespaces)
        {
            return RequestHelper.EnumTransclusionsAsync(Site, Title, namespaces);
        }


        #endregion

        #region Modification

        /// <inheritdoc cref="UpdateContentAsync(string,bool,bool,AutoWatchBehavior,CancellationToken)"/>
        public Task UpdateContentAsync(string summary)
        {
            return UpdateContentAsync(summary, false, true, AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <inheritdoc cref="UpdateContentAsync(string,bool,bool,AutoWatchBehavior,CancellationToken)"/>
        public Task UpdateContentAsync(string summary, bool minor)
        {
            return UpdateContentAsync(summary, minor, true, AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <inheritdoc cref="UpdateContentAsync(string,bool,bool,AutoWatchBehavior,CancellationToken)"/>
        public Task UpdateContentAsync(string summary, bool minor, bool bot)
        {
            return UpdateContentAsync(summary, minor, bot, AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <inheritdoc cref="UpdateContentAsync(string,bool,bool,AutoWatchBehavior,CancellationToken)"/>
        public Task<bool> UpdateContentAsync(string summary, bool minor, bool bot, AutoWatchBehavior watch)
        {
            return UpdateContentAsync(summary, minor, bot, watch, new CancellationToken());
        }

        /// <summary>
        /// Submits content contained in <see cref="Content"/>, making edit to the page.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <param name="summary">The edit summary. Leave it blank to use the default edit summary.</param>
        /// <param name="minor">Whether the edit is a minor edit. (See <a href="https://meta.wikimedia.org/wiki/Help:Minor_edit">m:Help:Minor Edit</a>)</param>
        /// <param name="bot">Whether to mark the edit as bot; even if you are using a bot account the edits will not be marked unless you set this flag.</param>
        /// <param name="watch">Specify how the watchlist is affected by this edit.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns><c>true</c> if page content has been changed; <c>false</c> otherwise.</returns>
        /// <remarks>
        /// This action will refill <see cref="Id" />, <see cref="Title"/>,
        /// <see cref="ContentModel"/>, <see cref="LastRevisionId"/>, and invalidate
        /// <see cref="ContentLength"/>, <see cref="LastRevision"/>, and <see cref="LastTouched"/>.
        /// You should call <see cref="RefreshAsync()"/> again if you're interested in them.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Cannot create actual page in the specified namespace.</exception>
        /// <exception cref="OperationConflictException">Edit conflict detected.</exception>
        /// <exception cref="UnauthorizedOperationException">You have no rights to edit the page.</exception>
        public async Task<bool> UpdateContentAsync(string summary, bool minor, bool bot, AutoWatchBehavior watch,
            CancellationToken cancellationToken)
        {
            using (Site.BeginActionScope(this))
            using (await Site.ModificationThrottler.QueueWorkAsync("Edit: " + this, cancellationToken))
            {
                // When passing this to the Edit API, always pass the token parameter last
                // (or at least after the text parameter). That way, if the edit gets interrupted,
                // the token won't be passed and the edit will fail.
                // This is done automatically by mw.Api.
                JToken jresult;
                try
                {
                    jresult = await Site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "edit",
                        token = WikiSiteToken.Edit,
                        title = Title,
                        minor = minor,
                        bot = bot,
                        recreate = true,
                        maxlag = 5,
                        basetimestamp = LastRevision?.TimeStamp,
                        watchlist = watch,
                        summary = summary,
                        text = Content,
                    }), cancellationToken);
                }
                catch (OperationFailedException ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case "protectedpage":
                            throw new UnauthorizedOperationException(ex);
                        case "pagecannotexist":
                            throw new InvalidOperationException(ex.ErrorMessage, ex);
                        default:
                            throw;
                    }
                }
                var jedit = jresult["edit"];
                var result = (string) jedit["result"];
                if (result == "Success")
                {
                    if (jedit["nochange"] != null)
                    {
                        Site.Logger.LogInformation("Submitted empty edit to page.");
                        return false;
                    }
                    ContentModel = (string) jedit["contentmodel"];
                    LastRevisionId = (int) jedit["newrevid"];
                    Id = (int) jedit["pageid"];
                    Title = (string) jedit["title"];
                    Site.Logger.LogInformation("Edited page. New revid={RevisionId}.", LastRevisionId);
                    return true;
                }
                // No "errors" in json result but result is not Success.
                throw new OperationFailedException(result, (string) null);
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
        public async Task MoveAsync(string newTitle, string reason, PageMovingOptions options, AutoWatchBehavior watch,
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
                    jresult = await Site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "move",
                        token = WikiSiteToken.Move,
                        from = Title,
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
                            if (ex.ErrorCode.StartsWith("cantmove"))
                                throw new UnauthorizedOperationException(ex);
                            throw;
                    }
                }
                var fromTitle = (string) jresult["move"]["from"];
                var toTitle = (string) jresult["move"]["to"];
                Site.Logger.LogInformation("Page [[{fromTitle}]] has been moved to [[{toTitle}]].", fromTitle, toTitle);
                Title = toTitle;
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
            return DeleteAsync(reason, watch, new CancellationToken());
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
                    jresult = await Site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "delete",
                        token = WikiSiteToken.Delete,
                        title = Title,
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
                var title = (string) jresult["delete"]["title"];
                Exists = false;
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
            return PurgeAsync(options, new CancellationToken());
        }

        /// <summary>
        /// Asynchronously purges the current page with the given options.
        /// </summary>
        /// <returns><c>true</c> if the page has been successfully purged.</returns>
        public async Task<bool> PurgeAsync(PagePurgeOptions options, CancellationToken cancellationToken)
        {
            var failure = await RequestHelper.PurgePagesAsync(new[] {this}, options, cancellationToken);
            return failure.Count == 0;
        }

        #endregion

        /// <summary>
        /// Gets a initialized <see cref="WikiPageStub"/> from the current instance.
        /// </summary>
        public WikiPageStub ToPageStub() => new WikiPageStub(Id, Title, NamespaceId);

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Title) ? ("#" + Id) : Title;
        }
    }

    /// <summary>
    /// Specifies wheter to watch the page after editing it.
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
    /// </summary>
    [Flags]
    public enum PageQueryOptions
    {
        None = 0,

        /// <summary>
        /// Fetch content of the page.
        /// </summary>
        FetchContent = 1,

        /// <summary>
        /// Resolves directs automatically. This may later change <see cref="WikiPage.Title"/>.
        /// This option cannot be used with generators.
        /// In the case of multiple redirects, all redirects will be resolved.
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
        public string Type { get; private set; }

        [JsonProperty]
        public string Level { get; private set; }

        public DateTime Expiry { get; private set; }

        [JsonProperty]
        public bool Cascade { get; private set; }

        [JsonProperty("expiry")]
        private string ExpiryProxy
        {
            set { Expiry = MediaWikiUtility.ParseDateTimeOffset(value); }
        }

        /// <summary>
        /// 返回该实例的完全限定类型名。
        /// </summary>
        /// <returns>
        /// 包含完全限定类型名的 <see cref="T:System.String"/>。
        /// </returns>
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
        public bool Disambiguation => this["disambiguation"] != null;

        public string DisplayTitle => (string)this["displaytitle"];

        public string PageImage => (string)this["page_image"];

        public bool IsHiddenCategory => this["hiddencat"] != null;
    }
}
