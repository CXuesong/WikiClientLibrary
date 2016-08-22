using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a page on MediaWiki site.
    /// </summary>
    public class Page
    {
        public Page(Site site, string title)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));
            Site = site;
            WikiClient = Site.WikiClient;
            Debug.Assert(WikiClient != null);
            Title = title;
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (title == null) throw new ArgumentNullException(nameof(title));
        }

        public WikiClient WikiClient { get; }

        public Site Site { get; }

        public int Id { get; private set; }

        public int NamespaceId { get; private set; }

        public int LastRevisionId { get; private set; }

        public int ContentLength { get; private set; }

        /// <summary>
        /// Page touched timestamp.
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
        /// </summary>
        public bool Exists { get; private set; }

        /// <summary>
        /// Content model. (MediaWiki 1.22)
        /// </summary>
        public string ContentModel { get; private set; }

        /// <summary>
        /// Page language. (MediaWiki 1.24)
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:PageLanguage .</remarks>
        public string PageLanguage { get; private set; }

        /// <summary>
        /// Gets the title of page. When more information is available,
        /// gets the normalized title of page.
        /// </summary>
        /// <remarks>
        /// Normalized title is a title with underscores(_) replaced by spaces,
        /// and the first letter is usually upper-case.
        /// </remarks>
        public string Title { get; private set; }

        /// <summary>
        /// Gets / Sets the content of the page.
        /// </summary>
        /// <remarks>You should have invoked <see cref="RefreshContentAsync"/> before trying to read the content of the page.</remarks>
        public string Content { get; set; }

        /// <summary>
        /// Gets the latest revision of the page.
        /// </summary>
        /// <remarks>Make sure to invoke <see cref="RefreshContentAsync"/> before getting the value.</remarks>
        public Revision LastRevision { get; private set; }

        private static bool AreIdEquals(int id1, int id2)
        {
            if (id1 == id2) return false;
            // For inexistent pages, id is negative.
            if (id2 > 0 && id1 > 0 || Math.Sign(id2) != Math.Sign(id1)) return true;
            return false;
        }

        #region Query

        /// <summary>
        /// Loads page information from JSON.
        /// </summary>
        /// <param name="prop">query.pages.xxx property.</param>
        /// <param name="loadFullInfo">Whether to attempt to load full page information, including protection.</param>
        internal void LoadPageInfo(JProperty prop, bool loadFullInfo)
        {
            var id = Convert.ToInt32(prop.Name);
            // I'm not sure whether this assertion holds.
            Debug.Assert(id != 0);
            // The page has been overwritten, or deleted.
            if (Id != 0 && !AreIdEquals(Id, id))
                WikiClient.Logger?.Warn($"Detected page id changed: {Title}, {Id}");
            Id = id;
            var page = prop.Value;
            Title = (string) page["title"];
            // Invalid page title (like Special:)
            if (page["invalid"] != null)
            {
                var reason = (string) page["invalidreason"];
                throw new OperationFailedException(reason);
            }
            NamespaceId = (int) page["ns"];
            Exists = page["missing"] == null;
            ContentModel = (string) page["contentmodel"];
            PageLanguage = (string) page["pagelanguage"];
            if (Exists)
            {
                ContentLength = (int) page["length"];
                LastRevisionId = (int) page["lastrevid"];
                LastTouched = (DateTime) page["touched"];
                if (loadFullInfo)
                {
                    Protections = ((JArray) page["protection"]).ToObject<IReadOnlyCollection<ProtectionInfo>>(
                        Utility.WikiJsonSerializer);
                    RestrictionTypes = ((JArray) page["restrictiontypes"])?.ToObject<IReadOnlyCollection<string>>(
                        Utility.WikiJsonSerializer);
                }
            }
            else
            {
                ContentLength = 0;
                LastRevisionId = 0;
                LastTouched = DateTime.MinValue;
                if (loadFullInfo)
                {
                    Protections = null;
                    RestrictionTypes = null;
                }
            }
        }

        /// <summary>
        /// Loads last revision from JSON.
        /// </summary>
        /// <param name="revision">query.pages.revisions.xxx property.</param>
        internal void LoadLastRevision(JObject revision)
        {
            LastRevision = revision.ToObject<Revision>(Utility.WikiJsonSerializer);
            LastRevisionId = LastRevision.Id;
            Content = LastRevision.Content;
        }

        /// <summary>
        /// Fetch basic information of the page.
        /// </summary>
        public async Task RefreshInfoAsync()
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                prop = "info",
                inprop = "protection",
                titles = Title
            });
            LoadPageInfo(((JObject) jobj["query"]["pages"]).Properties().First(), true);
        }

        /// <summary>
        /// Fetch the latest revision and content of the page.
        /// </summary>
        public async Task RefreshContentAsync()
        {
            var needFullRefresh = Id == 0;
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                prop = "info|revisions",
                rvprop = "ids|timestamp|flags|comment|user|contentmodel|sha1|content",
                inprop = needFullRefresh ? "protection" : null,
                maxlag = 5,
                titles = Title
            });
            // TODO Cache content
            var prop = ((JObject) jobj["query"]["pages"]).Properties().First();
            //var newId = Convert.ToInt32(prop.Name);
            // Update page info by the way.
            LoadPageInfo(prop, needFullRefresh);
            var rev = (JObject) prop.Value["revisions"]?.FirstOrDefault();
            if (rev != null)
            {
                LoadLastRevision(rev);
            }
            else
            {
                LastRevision = null;
                LastRevisionId = 0;
            }
        }

        #endregion

        #region Modification

        /// <summary>
        /// Submits content contained in <see cref="Content"/>, making edit to the page.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <remarks>
        /// This action will refill <see cref="Id" />, <see cref="Title"/>,
        /// <see cref="ContentModel"/>, <see cref="LastRevisionId"/>, and invalidates
        /// <see cref="ContentLength"/>, <see cref="LastRevision"/>, and <see cref="LastTouched"/>.
        /// You should call <see cref="RefreshInfoAsync"/> or <see cref="RefreshContentAsync"/> again
        /// if you're interested in them.
        /// </remarks>
        public Task UpdateContentAsync(string summary)
        {
            return UpdateContentAsync(summary, false, true, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Submits content contained in <see cref="Content"/>, making edit to the page.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <remarks>
        /// This action will refill <see cref="Id" />, <see cref="Title"/>,
        /// <see cref="ContentModel"/>, <see cref="LastRevisionId"/>, and invalidates
        /// <see cref="ContentLength"/>, <see cref="LastRevision"/>, and <see cref="LastTouched"/>.
        /// You should call <see cref="RefreshInfoAsync"/> or <see cref="RefreshContentAsync"/> again
        /// if you're interested in them.
        /// </remarks>
        public Task UpdateContentAsync(string summary, bool minor)
        {
            return UpdateContentAsync(summary, minor, true, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Submits content contained in <see cref="Content"/>, making edit to the page.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <remarks>
        /// This action will refill <see cref="Id" />, <see cref="Title"/>,
        /// <see cref="ContentModel"/>, <see cref="LastRevisionId"/>, and invalidates
        /// <see cref="ContentLength"/>, <see cref="LastRevision"/>, and <see cref="LastTouched"/>.
        /// You should call <see cref="RefreshInfoAsync"/> or <see cref="RefreshContentAsync"/> again
        /// if you're interested in them.
        /// </remarks>
        public Task UpdateContentAsync(string summary, bool minor, bool bot)
        {
            return UpdateContentAsync(summary, minor, bot, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Submits content contained in <see cref="Content"/>, making edit to the page.
        /// (MediaWiki 1.16)
        /// </summary>
        /// <remarks>
        /// This action will refill <see cref="Id" />, <see cref="Title"/>,
        /// <see cref="ContentModel"/>, <see cref="LastRevisionId"/>, and invalidates
        /// <see cref="ContentLength"/>, <see cref="LastRevision"/>, and <see cref="LastTouched"/>.
        /// You should call <see cref="RefreshInfoAsync"/> or <see cref="RefreshContentAsync"/> again
        /// if you're interested in them.
        /// </remarks>
        /// <exception cref="OperationConflictException">Edit conflict detected.</exception>
        /// <exception cref="UnauthorizedOperationException">You have no rights to edit the page.</exception>
        public async Task UpdateContentAsync(string summary, bool minor, bool bot, AutoWatchBehavior watch)
        {
            var tokenTask = Site.GetTokenAsync("edit");
            await WikiClient.WaitForThrottleAsync();
            var token = await tokenTask;
            // When passing this to the Edit API, always pass the token parameter last
            // (or at least after the text parameter). That way, if the edit gets interrupted,
            // the token won't be passed and the edit will fail.
            // This is done automatically by mw.Api.
            JToken jresult;
            try
            {
                jresult = await WikiClient.GetJsonAsync(new
                {
                    action = "edit",
                    title = Title,
                    minor = minor,
                    bot = bot,
                    recreate = true,
                    maxlag = 5,
                    basetimestamp = LastRevision?.TimeStamp,
                    watchlist = watch,
                    summary = summary,
                    text = Content,
                    token = token,
                });
            }
            catch (OperationFailedException ex)
            {
                switch (ex.ErrorCode)
                {
                    case "protectedpage":
                        throw new UnauthorizedOperationException(ex);
                    default:
                        throw;
                }
            }
            var jedit = jresult["edit"];
            var result = (string) jedit["result"];
            if (result == "Success")
            {
                ContentModel = (string) jedit["contentmodel"];
                LastRevisionId = (int) jedit["newrevid"];
                Id = (int) jedit["pageid"];
                Title = (string) jedit["title"];
            }
            else
            {
                throw new OperationFailedException(result, (string) null);
            }
        }

        #endregion

#region Management

        /// <summary>
        /// Get token and wait for a while.
        /// </summary>
        private async Task<string> GetTokenAndWaitAsync(string tokenType)
        {
            var tokenTask = Site.GetTokenAsync("csrf");
            await WikiClient.WaitForThrottleAsync();
            return await tokenTask;
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public Task MoveAsync(string newTitle, string reason, PageMovingOptions options)
        {
            return MoveAsync(newTitle, reason, options, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public Task MoveAsync(string newTitle, string reason)
        {
            return MoveAsync(newTitle, reason, PageMovingOptions.None, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public Task MoveAsync(string newTitle)
        {
            return MoveAsync(newTitle, null, PageMovingOptions.None, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Moves (renames) a page. (MediaWiki 1.12)
        /// </summary>
        public async Task MoveAsync(string newTitle, string reason, PageMovingOptions options, AutoWatchBehavior watch)
        {
            if (newTitle == null) throw new ArgumentNullException(nameof(newTitle));
            if (newTitle == Title) return;
            var token = await GetTokenAndWaitAsync("csrf");
            // When passing this to the Edit API, always pass the token parameter last
            // (or at least after the text parameter). That way, if the edit gets interrupted,
            // the token won't be passed and the edit will fail.
            // This is done automatically by mw.Api.
            JToken jresult;
            try
            {
                jresult = await WikiClient.GetJsonAsync(new
                {
                    action = "move",
                    from = Title,
                    to = newTitle,
                    maxlag = 5,
                    movetalk = (options & PageMovingOptions.LeaveTalk) != PageMovingOptions.LeaveTalk,
                    movesubpages = (options & PageMovingOptions.MoveSubpages) == PageMovingOptions.MoveSubpages,
                    noredirect = (options & PageMovingOptions.NoRedirect) == PageMovingOptions.NoRedirect,
                    ignorewarnings = (options & PageMovingOptions.IgnoreWarnings) == PageMovingOptions.IgnoreWarnings,
                    watchlist = watch,
                    reason = reason,
                    token = token,
                });
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
            var fromTitle = (string) jresult["move"]["to"];
            var toTitle = (string)jresult["move"]["to"];
            Site.Logger.Info($"Page {fromTitle} has been moved to {toTitle} .");
            Title = toTitle;
        }

        /// <summary>
        /// Deletes the current page.
        /// </summary>
        public Task<bool> DeleteAsync(string reason)
        {
            return DeleteAsync(reason, AutoWatchBehavior.Default);
        }

        /// <summary>
        /// Deletes the current page.
        /// </summary>
        public async Task<bool> DeleteAsync(string reason, AutoWatchBehavior watch)
        {
            var token = await GetTokenAndWaitAsync("csrf");
            JToken jresult;
            try
            {
                jresult = await WikiClient.GetJsonAsync(new
                {
                    action = "delete",
                    title = Title,
                    maxlag = 5,
                    watchlist = watch,
                    reason = reason,
                    token = token,
                });
            }
            catch (OperationFailedException ex)
            {
                // Couldn't delete "title". Maybe it was deleted already by someone else
                switch (ex.ErrorCode)
                {
                    case "cantdelete":
                        return false;
                    case "permissiondenied":
                        throw new UnauthorizedOperationException(ex);
                }
                throw;
            }
            var title = (string)jresult["delete"]["title"];
            Site.Logger.Info($"Page {title} has been deleted.");
            return true;
        }

        /// <summary>
        /// Purges the current page.
        /// </summary>
        /// <returns><c>true</c> if the page has been successfully purged.</returns>
        public async Task<bool> PurgeAsync()
        {
            JToken jresult;
            try
            {
                jresult = await WikiClient.GetJsonAsync(new
                {
                    action = "purge",
                    titles = Title,
                    forcelinkupdate = true,
                    forcerecursivelinkupdate = true,
                });
            }
            catch (OperationFailedException ex)
            {
                if (ex.ErrorCode == "cantpurge") throw new UnauthorizedOperationException(ex);
                throw;
            }
            var page = jresult["purge"].First();
            return page["purged"] != null;
        }

        #endregion

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return Title;
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
    /// Page protection information.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ProtectionInfo
    {
        public string Type { get; private set; }

        public string Level { get; private set; }

        public DateTime Expiry { get; private set; }

        public bool Cascade { get; private set; }

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
    /// Represents a revision of a page.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Revision
    {
        [JsonProperty("revid")]
        public int Id { get; private set; }

        [JsonProperty]
        public int ParentId { get; private set; }

        [JsonProperty("*")]
        public string Content { get; private set; }

        [JsonProperty]
        public string Comment { get; private set; }

        [JsonProperty]
        public string ContentModel { get; private set; }

        [JsonProperty]
        public string Sha1 { get; private set; }

        [JsonProperty]
        public string UserId { get; private set; }

        [JsonProperty("user")]
        public string UserName { get; private set; }

        /// <summary>
        /// The timestamp of revision.
        /// </summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// 返回该实例的完全限定类型名。
        /// </summary>
        /// <returns>
        /// 包含完全限定类型名的 <see cref="T:System.String"/>。
        /// </returns>
        public override string ToString()
        {
            return $"Revision#{Id}, SHA1={Sha1}";
        }
    }
}
