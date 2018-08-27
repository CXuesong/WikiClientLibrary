using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Infrastructures.Logging;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public partial class WikiSite : IWikiClientLoggable, IWikiClientAsyncInitialization
    {

        private readonly SiteOptions options;

        /// <summary>
        /// Gets the options applied to the current instance.
        /// </summary>
        /// <remarks>
        /// The value is cloned from the <see cref="SiteOptions"/> passed into constructor.
        /// </remarks>
        protected SiteOptions Options => options;

        #region Services

        /// <summary>
        /// Gets the MediaWiki API client used to perform the requests.
        /// </summary>
        public IWikiClient WikiClient { get; }

        /// <summary>
        /// A handler used to re-login when account assertion fails.
        /// </summary>
        public IAccountAssertionFailureHandler AccountAssertionFailureHandler { get; set; }

        private Throttler _ModificationThrottler;
        private readonly TokensManager tokensManager;

        /// <summary>
        /// A throttler used to enforce the speed limitation when performing edit/move/delete operations.
        /// </summary>
        public Throttler ModificationThrottler
        {
            get
            {
                if (_ModificationThrottler != null) return _ModificationThrottler;
                var t = new Throttler { Logger = Logger };
                Volatile.Write(ref _ModificationThrottler, t);
                return t;
            }
            set { _ModificationThrottler = value; }
        }

        #endregion

        /// <summary>
        /// Given a site or page URL of a MediaWiki site, try to look for the Api Endpoint URL of it.
        /// </summary>
        /// <param name="client">WikiClient instance.</param>
        /// <param name="urlExpression">The URL of MediaWiki site. It can be with or without protocol prefix.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="urlExpression"/> is <c>null</c>.</exception>
        /// <exception cref="TimeoutException">A time-out has been reached during test requests.</exception>
        /// <returns>The URL of Api Endpoint. OR <c>null</c> if such search has failed.</returns>
        public static Task<string> SearchApiEndpointAsync(WikiClient client, string urlExpression)
        {
            return MediaWikiUtility.SearchApiEndpointAsync(client, urlExpression);
        }

        /// <inheritdoc cref="WikiSite(IWikiClient,SiteOptions,string,string)"/>
        /// <summary>
        /// Initializes a <see cref="WikiSite"/> instance with the specified API endpoint.
        /// </summary>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to use <see cref="WikiSite(IWikiClient,SiteOptions,string, string)"/> to login before any other API requests.</exception>
        /// <remarks></remarks>
        public WikiSite(IWikiClient wikiClient, string apiEndpoint)
          : this(wikiClient, new SiteOptions(apiEndpoint), null, null)
        {

        }

        /// <inheritdoc cref="WikiSite(IWikiClient,SiteOptions,string,string)"/>
        /// <summary>
        /// Initializes a <see cref="WikiSite"/> instance with the specified settings.
        /// </summary>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to use <see cref="WikiSite(IWikiClient,SiteOptions,string, string)"/> to login before any other API requests.</exception>
        /// <remarks></remarks>
        public WikiSite(IWikiClient wikiClient, SiteOptions options)
            : this(wikiClient, options, null, null)
        {

        }

        /// <summary>
        /// Initializes a <see cref="WikiSite"/> instance with the specified settings
        /// and optional login before fetching for site information.
        /// </summary>
        /// <param name="wikiClient">WikiClient instance.</param>
        /// <param name="options">Site options.</param>
        /// <param name="userName">The user name used to login before fetching for site information. Pass <c>null</c> to fetch site information without login first.</param>
        /// <param name="password">The password used to login before fetching for site information.</param>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> or <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">One or more settings in <paramref name="options"/> is invalid.</exception>
        /// <remarks>
        /// <para>For the private wiki where anonymous users cannot access query API, you can use this
        /// overload to login to the site before any querying API invocations are issued.</para>
        /// </remarks>
        public WikiSite(IWikiClient wikiClient, SiteOptions options, string userName, string password)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.ApiEndpoint))
                throw new ArgumentException("Invalid API endpoint url.", nameof(options));
            WikiClient = wikiClient;
            this.options = options.Clone();
            tokensManager = new TokensManager(this);
            DisambiguationTemplatesAsync = new AsyncLazy<ICollection<string>>(async () =>
            {
                if (this.options.DisambiguationTemplates == null)
                {
                    var dabPages = await RequestHelper
                        .EnumLinksAsync(this, "MediaWiki:Disambiguationspage", new[] { BuiltInNamespaces.Template })
                        .ToList();
                    if (dabPages.Count == 0)
                    {
                        // Try to fetch from mw messages
                        var msg = await GetMessageAsync("disambiguationspage");
                        if (msg != null) dabPages.Add(msg);
                    }
                    dabPages.Add(SiteOptions.DefaultDisambiguationTemplate);
                    return dabPages;
                }
                return this.options.DisambiguationTemplates;
            });

            async Task InitializeAsync()
            {
                if (userName != null)
                {
                    await LoginAsync(userName, password);
                    await RefreshSiteInfoAsync();
                }
                else
                {
                    var refSi = RefreshSiteInfoAsync();
                    var refAi = RefreshAccountInfoAsync();
                    await refSi;
                    await refAi;
                }
            }

            localInitialization = InitializeAsync();
            Initialization = localInitialization;
        }

        /// <summary>
        /// Refreshes site information.
        /// </summary>
        /// <returns>
        /// This method affects <see cref="SiteInfo"/>, <see cref="Namespaces"/>,
        /// <see cref="InterwikiMap"/>, and <see cref="Extensions"/> properties.
        /// </returns>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to login first.</exception>
        public virtual async Task RefreshSiteInfoAsync()
        {
            using (this.BeginActionScope(null))
            {
                var jobj = await InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                {
                    action = "query",
                    meta = "siteinfo",
                    siprop = "general|namespaces|namespacealiases|interwikimap|extensions"
                }), true, CancellationToken.None);
                var qg = (JObject)jobj["query"]["general"];
                var ns = (JObject)jobj["query"]["namespaces"];
                var aliases = (JArray)jobj["query"]["namespacealiases"];
                var interwiki = (JArray)jobj["query"]["interwikimap"];
                var extensions = (JArray)jobj["query"]["extensions"];
                _SiteInfo = qg.ToObject<SiteInfo>(Utility.WikiJsonSerializer);
                _Namespaces = new NamespaceCollection(this, ns, aliases);
                _InterwikiMap = new InterwikiMap(this, interwiki, _Logger);
                _Extensions = new ExtensionCollection(this, extensions);
            }
        }

        /// <summary>
        /// Refreshes user account information.
        /// </summary>
        /// <remarks>
        /// This method affects <see cref="AccountInfo"/> property.
        /// </remarks>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to login first.</exception>
        public async Task RefreshAccountInfoAsync()
        {
            // Note: _SiteInfo can be null here.
            using (this.BeginActionScope(null))
            {
                var jobj = await InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                {
                    action = "query",
                    meta = "userinfo",
                    uiprop = "blockinfo|groups|hasmsg|rights"
                }), true, CancellationToken.None);
                _AccountInfo = ((JObject)jobj["query"]["userinfo"]).ToObject<AccountInfo>(Utility.WikiJsonSerializer);
                ListingPagingSize = _AccountInfo.HasRight(UserRights.ApiHighLimits) ? 5000 : 500;
            }
        }

        private SiteInfo _SiteInfo;
        private AccountInfo _AccountInfo;
        private NamespaceCollection _Namespaces;
        private InterwikiMap _InterwikiMap;
        private ExtensionCollection _Extensions;

        /// <summary>
        /// Gets the basic site information.
        /// </summary>
        public SiteInfo SiteInfo
        {
            get
            {
                AsyncInitializationHelper.EnsureInitialized(typeof(WikiSite), localInitialization);
                return _SiteInfo;
            }
        }

        /// <summary>
        /// Gets the currrent user's account information.
        /// </summary>
        public AccountInfo AccountInfo
        {
            get
            {
                AsyncInitializationHelper.EnsureInitialized(typeof(WikiSite), localInitialization);
                var ai = _AccountInfo;
                // User has logged out.
                if (ai == null) throw new InvalidOperationException("The AccountInfo is not initialized.");
                return _AccountInfo;
            }
        }

        /// <summary>
        /// Gets a collection of namespaces used on this MediaWiki site.
        /// </summary>
        public NamespaceCollection Namespaces
        {
            get
            {
                AsyncInitializationHelper.EnsureInitialized(typeof(WikiSite), localInitialization);
                return _Namespaces;
            }
        }

        /// <summary>
        /// Gets the interwiki map information on this wiki site.
        /// </summary>
        public InterwikiMap InterwikiMap
        {
            get
            {
                AsyncInitializationHelper.EnsureInitialized(typeof(WikiSite), localInitialization);
                return _InterwikiMap;
            }
        }

        /// <summary>
        /// Gets a collection of installed extensions on this MediaWiki site.
        /// </summary>
        public ExtensionCollection Extensions
        {
            get
            {
                AsyncInitializationHelper.EnsureInitialized(typeof(WikiSite), localInitialization);
                return _Extensions;
            }
        }

        /// <summary>
        /// The endpoint URL for MediaWiki API.
        /// </summary>
        public string ApiEndpoint => options.ApiEndpoint;

        /// <summary>
        /// Gets the default result limit per page for current user.
        /// </summary>
        /// <value>This value is 500 for user, and 5000 for bots.</value>
        // Use 500 for default. This value will be updated along with UserInfo.
        internal int ListingPagingSize { get; private set; } = 500;

        /// <summary>
        /// Gets a list of titles of disambiguation templates. The default DAB template title
        /// will NOT be included in the list.
        /// </summary>
        internal readonly AsyncLazy<ICollection<string>> DisambiguationTemplatesAsync;

        #region Basic API

        /// <inheritdoc cref="InvokeMediaWikiApiAsync{T}(WikiRequestMessage,IWikiResponseMessageParser{T},bool,CancellationToken)"/>
        /// <remarks>This overload uses <see cref="MediaWikiJsonResponseParser.Default"/> as response parser.</remarks>
        public Task<JToken> InvokeMediaWikiApiAsync(WikiRequestMessage message, CancellationToken cancellationToken)
        {
            return InvokeMediaWikiApiAsync(message, MediaWikiJsonResponseParser.Default, false, cancellationToken);
        }

        /// <inheritdoc cref="InvokeMediaWikiApiAsync{T}(WikiRequestMessage,IWikiResponseMessageParser{T},bool,CancellationToken)"/>
        /// <remarks>This overload uses <see cref="MediaWikiJsonResponseParser.Default"/> as response parser.</remarks>
        public Task<JToken> InvokeMediaWikiApiAsync(WikiRequestMessage message,
            bool suppressAccountAssertion, CancellationToken cancellationToken)
        {
            return InvokeMediaWikiApiAsync(message, MediaWikiJsonResponseParser.Default, suppressAccountAssertion, cancellationToken);
        }

        /// <summary>
        /// Invokes MediaWiki API and gets JSON result.
        /// </summary>
        /// <param name="message">The request message.</param>
        /// <param name="responseParser">The parser that checks and parses the API response into <see cref="JToken"/>.</param>
        /// <param name="suppressAccountAssertion">Whether to temporarily disable account assertion as set in <see cref="SiteOptions.AccountAssertion"/>.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationFailedException">There is "error" node in returned JSON. Instances of dervied types may be thrown.</exception>
        /// <exception cref="AccountAssertionFailureException">You enabled account assertion, the assertion failed, and it also failed to retry logging in.</exception>
        /// <returns>A task that returns the JSON response when completed.</returns>
        /// <remarks>
        /// Some enhancements are available only if <paramref name="message"/> is <see cref="MediaWikiFormRequestMessage"/>, including
        /// <list type="bullet">
        /// <item><description>
        /// Account assertion, as specified in <see cref="SiteOptions.AccountAssertion"/>.
        /// </description></item>
        /// <item><description>
        /// Automatic token-refreshing on <c>badtoken</c> error. This requires you to set all your <c>token</c>
        /// fields in the <paramref name="message"/> to a placeholder of type <see cref="WikiSiteToken"/>,
        /// instead of the actual token string.
        /// </description></item>
        /// </list>
        /// </remarks>
        public async Task<T> InvokeMediaWikiApiAsync<T>(WikiRequestMessage message, IWikiResponseMessageParser<T> responseParser,
            bool suppressAccountAssertion, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (responseParser == null) throw new ArgumentNullException(nameof(responseParser));
            var form = message as MediaWikiFormRequestMessage;
            var localRequest = message;
            var badTokenRetries = 0;
            RETRY:
            if (form != null)
            {
                // Apply tokens
                var newFields = new List<KeyValuePair<string, object>>(form.Fields.Count + 3)
                {
                    new KeyValuePair<string, object>("format", "json")
                };
                foreach (var tokenField in form.Fields)
                {
                    var value = tokenField.Value;
                    if (value is WikiSiteToken)
                        value = await tokensManager.GetTokenAsync(
                            ((WikiSiteToken)tokenField.Value).Type, false, cancellationToken);
                    newFields.Add(new KeyValuePair<string, object>(tokenField.Key, value));
                }
                // Apply account assertions
                if (!suppressAccountAssertion && _AccountInfo != null)
                {
                    if ((options.AccountAssertion & AccountAssertionBehavior.AssertBot) ==
                        AccountAssertionBehavior.AssertBot && _AccountInfo.IsBot)
                        newFields.Add(new KeyValuePair<string, object>("assert", "bot"));
                    else if ((options.AccountAssertion & AccountAssertionBehavior.AssertUser) ==
                             AccountAssertionBehavior.AssertUser && _AccountInfo.IsUser)
                        newFields.Add(new KeyValuePair<string, object>("assert", "user"));
                }
                localRequest = new MediaWikiFormRequestMessage(form.Id, newFields, form.AsMultipartFormData);
            }
            Logger.LogDebug("Sending request {Request}, SuppressAccountAssertion={SuppressAccountAssertion}",
                localRequest, suppressAccountAssertion);
            try
            {
                return await WikiClient.InvokeAsync(options.ApiEndpoint, localRequest, responseParser, cancellationToken);
            }
            catch (AccountAssertionFailureException)
            {
                if (AccountAssertionFailureHandler != null)
                {
                    // ISSUE Relogin might be called nultiple times.
                    if (reLoginTask == null)
                    {
                        Logger.LogWarning("Account assertion failed. Try to relogin.");
                        Volatile.Write(ref reLoginTask, Relogin());
                    }
                    else
                    {
                        Logger.LogWarning("Account assertion failed. Waiting for relongin.");
                    }
                    var result = await reLoginTask;
                    if (result) goto RETRY;
                }
                throw;
            }
            catch (BadTokenException)
            {
                // Allows retrying once.
                if (form == null || badTokenRetries >= 1) throw;
                string invalidatedToken = null;
                foreach (var tokenField in form.Fields.Where(p => p.Value is WikiSiteToken))
                {
                    invalidatedToken = ((WikiSiteToken)tokenField.Value).Type;
                    tokensManager.ClearCache(invalidatedToken);
                }
                if (invalidatedToken == null) throw;
                Logger.LogWarning("BadTokenException: {Request}. Will retry after invalidating the token: {Token}.",
                    message, invalidatedToken);
                badTokenRetries++;
                goto RETRY;
            }
        }

        #endregion

        #region Tokens

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        /// <exception cref="ArgumentException">Specified token type cannot be recognized.</exception>
        public Task<string> GetTokenAsync(string tokenType)
        {
            return GetTokenAsync(tokenType, false, CancellationToken.None);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        /// <exception cref="ArgumentException">Specified token type cannot be recognized.</exception>
        public Task<string> GetTokenAsync(string tokenType, bool forceRefetch)
        {
            return GetTokenAsync(tokenType, forceRefetch, CancellationToken.None);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        /// <exception cref="ArgumentException">Specified token type cannot be recognized.</exception>
        public Task<string> GetTokenAsync(string tokenType, CancellationToken cancellationToken)
        {
            return GetTokenAsync(tokenType, false, cancellationToken);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        /// <exception cref="ArgumentException">Specified token type cannot be recognized.</exception>
        public Task<string> GetTokenAsync(string tokenType, bool forceRefetch, CancellationToken cancellationToken)
        {
            return tokensManager.GetTokenAsync(tokenType, forceRefetch, cancellationToken);
        }

        #endregion

        #region Authentication

        private int isLoggingInOut = 0;

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public Task LoginAsync(string userName, string password)
        {
            return LoginAsync(userName, password, null, CancellationToken.None);
        }

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <param name="domain">Domain name. <c>null</c> is usually a good choice.</param>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public Task LoginAsync(string userName, string password, string domain)
        {
            return LoginAsync(userName, password, domain, CancellationToken.None);
        }

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <param name="domain">Domain name. <c>null</c> is usually a good choice.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public async Task LoginAsync(string userName, string password, string domain,
            CancellationToken cancellationToken)
        {
            // Note: this method may be invoked BEFORE the initialization of _SiteInfo.
            // Note: this method may be invoked upon initialization, so use _AccountInfo instead of AccountInfo.
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (Interlocked.Exchange(ref isLoggingInOut, 1) != 0)
                throw new InvalidOperationException("Cannot login/logout concurrently.");
            using (this.BeginActionScope(null, (object)userName))
            {
                try
                {
                    string token = null;
                    // For MedaiWiki 1.27+
                    if (_SiteInfo != null && _SiteInfo.Version >= new Version("1.27"))
                        token = await GetTokenAsync("login", true, cancellationToken);
                    // For MedaiWiki < 1.27, We'll have to request twice.
                    // If we are logging in before initialization of WikiSite, we just treat it the same as MedaiWiki < 1.27,
                    //  because the client might be logging to a private wiki,
                    //  where any "query" operation before logging-in might raise readapidenied error.
                    RETRY:
                    var jobj = await InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "login",
                        lgname = userName,
                        lgpassword = password,
                        lgtoken = token,
                        lgdomain = domain,
                    }), true, cancellationToken);
                    var result = (string)jobj["login"]["result"];
                    string message = null;
                    switch (result)
                    {
                        case "Success":
                            tokensManager.ClearCache();
                            await RefreshAccountInfoAsync();
                            Logger.LogInformation("Logged in as {Account}.", _AccountInfo);
                            Debug.Assert(_AccountInfo.IsUser,
                                "API result indicates the login is successful, but you are not currently in \"user\" group. Are you logging out on the other Site instance with the same API endpoint and the same WikiClient?");
                            return;
                        case "Aborted":
                            message =
                                "The login using the main account password (rather than a bot password) cannot proceed because user interaction is required. The clientlogin action should be used instead.";
                            break;
                        case "Throttled":
                            var time = (int)jobj["login"]["wait"];
                            Logger.LogWarning("{Site} login throttled: {Time}sec.", ToString(), time);
                            await Task.Delay(TimeSpan.FromSeconds(time), cancellationToken);
                            goto RETRY;
                        case "NeedToken":
                            token = (string)jobj["login"]["token"];
                            goto RETRY;
                        case "WrongToken": // We should have got correct token.
                            throw new UnexpectedDataException($"Unexpected login result: {result} .");
                    }
                    message = (string)jobj["login"]["reason"] ?? message;
                    throw new OperationFailedException(result, message);
                }
                finally
                {
                    Interlocked.Exchange(ref isLoggingInOut, 0);
                }
            }
        }

        /// <inheritdoc cref="LogoutAsync(bool)"/>
        /// <remarks></remarks>
        public Task LogoutAsync()
        {
            return LogoutAsync(false);
        }

        /// <summary>
        /// Logouts from the wiki site.
        /// </summary>
        /// <param name="invalidateAccountInfo">Whether to invalidate <see cref="AccountInfo"/>
        /// instead of calling <see cref="RefreshAccountInfoAsync"/> to retrieve the latest account information after logging out.</param>
        /// <remarks>If <paramref name="invalidateAccountInfo"/> is <c>true</c>,
        /// <see cref="AccountInfo"/> will be invalidated,
        /// and any attempt to read the property will raise <see cref="InvalidOperationException"/>
        /// until the next successful login.</remarks>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        public async Task LogoutAsync(bool invalidateAccountInfo)
        {
            if (Interlocked.Exchange(ref isLoggingInOut, 1) != 0)
                throw new InvalidOperationException("Cannot login/logout concurrently.");
            using (this.BeginActionScope(null))
            {
                try
                {
                    await SendLogoutRequestAsync();
                    tokensManager.ClearCache();
                    if (invalidateAccountInfo)
                        _AccountInfo = null;
                    else
                        await RefreshAccountInfoAsync();
                }
                finally
                {
                    Interlocked.Exchange(ref isLoggingInOut, 0);
                }
            }
        }

        /// <summary>
        /// Directly sends a logout request. This method will be invoked by <see cref="LogoutAsync(bool)"/>.
        /// </summary>
        /// <remarks>
        /// The default behavior is to send an <c>action=logout</c> MW API request.
        /// Derived classes may override this method to implement their own customized logout behavior.
        /// </remarks>
        protected virtual async Task SendLogoutRequestAsync()
        {
            Debug.Assert(isLoggingInOut == 1);
            var jobj = await InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "logout",
            }), true, CancellationToken.None);
        }

        private Task<bool> reLoginTask;
        private ILogger _Logger = NullLogger.Instance;

        private async Task<bool> Relogin()
        {
            Debug.Assert(AccountAssertionFailureHandler != null);
            var result = await AccountAssertionFailureHandler.Login(this);
            reLoginTask = null;
            return result;
        }

        #endregion

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.IsNullOrEmpty(_SiteInfo?.SiteName) ? options.ApiEndpoint : _SiteInfo.SiteName;
        }

        /// <inheritdoc />
        public ILogger Logger
        {
            get => _Logger;
            set => _Logger = value ?? NullLogger.Instance;
        }

        private readonly Task localInitialization;

        /// <inheritdoc />
        /// <remarks>
        /// For derived classes with their own asynchronous initialization logic,
        /// a. in asynchronous initialization task, await the value of this property set by the base class first,
        /// then do your own initialization work.
        /// b. in your class constructor, replace the value of this property with your combined initialization task.
        /// </remarks>
        public Task Initialization { get; protected set; }
    }

}

