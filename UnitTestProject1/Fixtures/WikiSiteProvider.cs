using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia;
using WikiClientLibrary.Wikia.Sites;

namespace WikiClientLibrary.Tests.UnitTestProject1.Fixtures;

/// <summary>
/// Provides cached <see cref="WikiSite"/> instance across the same UT class.
/// </summary>
/// <remarks>
/// This is a xUnit fixture intended to be shared within the same UT class. (no concurrency support)
/// </remarks>
public sealed class WikiSiteProvider : IAsyncDisposable
{

    private readonly HashSet<string> sitesNeedsLogin = new HashSet<string>();
    private readonly Dictionary<string, Task<WikiSite>> siteCache = new Dictionary<string, Task<WikiSite>>();
    private readonly WikiClient _WikiClient;

    public WikiSiteProvider()
    {
        if (!string.IsNullOrEmpty(CredentialManager.DirtyTestsEntryPointUrl))
            sitesNeedsLogin.Add(CredentialManager.DirtyTestsEntryPointUrl);
        // Ensures caller has a chance to initialize LoggerFactory before access WikiClient.
        _WikiClient = CreateWikiClient();
    }

    public void SiteNeedsLogin(string endpointUrl)
    {
        sitesNeedsLogin.Add(endpointUrl);
    }

    public WikiClient CreateWikiClient()
    {
        var client = new WikiClient
        {
            Timeout = TimeSpan.FromSeconds(20),
            RetryDelay = TimeSpan.FromSeconds(5),
#if ENV_CI_BUILD
                ClientUserAgent = $"UnitTest/1.0 ({RuntimeInformation.FrameworkDescription}; ENV_CI_BUILD)",
#else
            ClientUserAgent = $"UnitTest/1.0 ({RuntimeInformation.FrameworkDescription})",
#endif
        };
        return client;
    }

    private WikiClient GetWikiClient(ILoggerFactory loggerFactory)
    {
        loggerFactory.CreateLogger<WikiSiteProvider>().LogInformation("GetWikiClient returning cached WikiClient.");
        _WikiClient.Logger = loggerFactory.CreateLogger<WikiClient>();
        return _WikiClient;
    }

    /// <summary>
    /// Create or get a wiki site from local cache.
    /// </summary>
    public Task<WikiSite> GetWikiSiteAsync(string endpointUrl, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<WikiSiteProvider>();
        lock (siteCache)
        {
            if (!siteCache.TryGetValue(endpointUrl, out var task) || task.IsFaulted)
            {
                logger.LogInformation("Creating WikiSite instance for {EndpointUrl}.", endpointUrl);
                task = CreateWikiSiteAsync(GetWikiClient(loggerFactory), endpointUrl, loggerFactory, false);
                siteCache[endpointUrl] = task;
            }
            else if (task.IsCompleted)
            {
                logger.LogInformation("Reusing existing WikiSite instance for {EndpointUrl}.", endpointUrl);
                // Cached WikiSite. Replace logger.
#pragma warning disable VSTHRD103 // 当在异步方法中时，调用异步方法
                var site = task.Result;
#pragma warning restore VSTHRD103 // 当在异步方法中时，调用异步方法
                site.Logger = loggerFactory.CreateLogger(site.GetType());
            }
            return task;
        }
    }

    /// <summary>
    /// Create a wiki site, login if specified.
    /// </summary>
    public async Task<WikiSite> CreateWikiSiteAsync(IWikiClient wikiClient, string url, ILoggerFactory loggerFactory, bool noLogin)
    {
        WikiSite site;
        if (url.Contains(".wikia.com") || url.Contains(".wikia.org") || url.Contains(".fandom.com"))
        {
            var uri = new Uri(url, UriKind.Absolute);
            var rootUrl = new Uri(uri, ".").ToString();
            var options = new WikiaSiteOptions(rootUrl)
            {
                AccountAssertion = AccountAssertionBehavior.AssertAll,
            };
            site = new WikiaSite(wikiClient, options) { Logger = loggerFactory.CreateLogger<WikiaSite>() };
        }
        else
        {
            var options = new SiteOptions(url)
            {
                AccountAssertion = AccountAssertionBehavior.AssertAll,
            };
            site = new WikiSite(wikiClient, options) { Logger = loggerFactory.CreateLogger<WikiSite>() };
        }
        await site.Initialization;
        if (!noLogin && sitesNeedsLogin.Contains(url))
        {
            await CredentialManager.LoginAsync(site);
        }
        return site;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Logout all the sites.
        List<Task<WikiSite>> sites;
        lock (siteCache)
        {
            sites = siteCache.Values.ToList();
            sites.Clear();
        }
        foreach (var s in sites)
        {
            var site = await s;
            if (site.AccountInfo.IsUser)
                await site.LogoutAsync();
        }
    }
}