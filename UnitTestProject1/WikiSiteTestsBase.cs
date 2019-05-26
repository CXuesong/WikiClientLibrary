﻿// Enables the following conditional switch here
// to prevent test cases from making any edits.

//#define DRY_RUN

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia;
using WikiClientLibrary.Wikia.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{

    public class UnitTestsBase : IDisposable
    {

        public UnitTestsBase(ITestOutputHelper output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            Output = output;
            OutputLoggerFactory = new LoggerFactory();
            OutputLoggerFactory.AddProvider(new TestOutputLoggerProvider(Output));
        }

        public LoggerFactory OutputLoggerFactory { get; }

        public ITestOutputHelper Output { get; }

        protected void ShallowTrace(object obj, int depth = 2)
        {
            Output.WriteLine(Utility.DumpObject(obj, depth));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class WikiSiteTestsBase : UnitTestsBase
    {

        public WikiSiteTestsBase(ITestOutputHelper output) : base(output)
        {
            WikiClient = CreateWikiClient();
        }

        public WikiClient WikiClient { get; }

        private readonly HashSet<string> sitesNeedsLogin = new HashSet<string>();
        private readonly Dictionary<string, Task<WikiSite>> siteCache = new Dictionary<string, Task<WikiSite>>();

        protected void SiteNeedsLogin(string endpointUrl)
        {
            sitesNeedsLogin.Add(endpointUrl);
        }

        /// <summary>
        /// Create or get a wiki site from local cache.
        /// </summary>
        protected Task<WikiSite> GetWikiSiteAsync(string endpointUrl)
        {
            lock (siteCache)
            {
                if (!siteCache.TryGetValue(endpointUrl, out var task))
                {
                    task = CreateWikiSiteAsync(WikiClient, endpointUrl);
                    siteCache.Add(endpointUrl, task);
                }
                return task;
            }
        }

        /// <summary>
        /// Creates a <see cref="WikiSite"/> instance with a dedicated <see cref="WikiClient"/> instance.
        /// </summary>
        /// <remarks>This method can be handy for you to maul a certain WikiClient without affecting other WikiSite instances.</remarks>
        protected Task<WikiSite> CreateIsolatedWikiSiteAsync(string apiEndpoint)
        {
            var isolatedClient = CreateWikiClient();
            return CreateWikiSiteAsync(isolatedClient, apiEndpoint);
        }

        /// <summary>
        /// Create a wiki site, login if necessary.
        /// </summary>
        private async Task<WikiSite> CreateWikiSiteAsync(IWikiClient wikiClient, string url)
        {
            WikiSite site;
            if (url.Contains(".wikia.com") || url.Contains(".wikia.org") || url.Contains(".fandom.com"))
            {
                var uri = new Uri(url, UriKind.Absolute);
#if NETCOREAPP1_1
                var rootUrl = uri.ToString();
                rootUrl = rootUrl.Substring(0, rootUrl.IndexOf(uri.Authority, StringComparison.Ordinal) + uri.Authority.Length);
#else
                var rootUrl = uri.GetLeftPart(UriPartial.Authority);
#endif
                var options = new WikiaSiteOptions(rootUrl + "/")
                {
                    AccountAssertion = AccountAssertionBehavior.AssertAll,
                };
                site = new WikiaSite(wikiClient, options) { Logger = OutputLoggerFactory.CreateLogger<WikiaSite>() };
            }
            else
            {
                var options = new SiteOptions(url)
                {
                    AccountAssertion = AccountAssertionBehavior.AssertAll,
                };
                site = new WikiSite(wikiClient, options) { Logger = OutputLoggerFactory.CreateLogger<WikiSite>() };
            }
            await site.Initialization;
            if (sitesNeedsLogin.Contains(url))
            {
                await CredentialManager.LoginAsync(site);
            }
            return site;
        }

        /// <summary>
        /// Asserts that modifications to wiki site can be done in unit tests.
        /// </summary>
        protected static void AssertModify()
        {
#if DRY_RUN
            throw new SkipException("Remove #define DRY_RUN to perform edit tests.");
#endif
        }

        protected Task<WikiSite> WpTest2SiteAsync => GetWikiSiteAsync(Endpoints.WikipediaTest2);

        protected Task<WikiaSite> WikiaTestSiteAsync
        {
            get
            {
                async Task<WikiaSite> Cast()
                {
                    return (WikiaSite)await GetWikiSiteAsync(Endpoints.WikiaTest);
                }

                return Cast();
            }
        }

        protected Task<WikiSite> WpLzhSiteAsync => GetWikiSiteAsync(Endpoints.WikipediaLzh);

        protected Task<WikiSite> WpBetaSiteAsync => GetWikiSiteAsync(Endpoints.WikipediaBetaEn);

        protected Task<WikiSite> WikimediaCommonsBetaSiteAsync => GetWikiSiteAsync(Endpoints.WikimediaCommonsBeta);

        protected Task<WikiSite> WikidataSiteAsync => GetWikiSiteAsync(Endpoints.Wikidata);

        protected Task<WikiSite> WikidataTestSiteAsync => GetWikiSiteAsync(Endpoints.WikidataTest);

        protected Task<WikiSite> WikiSiteFromNameAsync(string sitePropertyName)
        {
            async Task<TDest> CastAsync<TSource, TDest>(Task<TSource> sourceTask)
            {
                return (TDest)(object)await sourceTask;
            }
            var task = GetType()
                .GetProperty(sitePropertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(this);
            if (task is Task<WikiSite> ws) return ws;
            if (task is Task<WikiaSite> was) return CastAsync<WikiaSite, WikiSite>(was);
            throw new NotSupportedException();
        }

        protected WikiClient CreateWikiClient()
        {
            var client = new WikiClient
            {
                Timeout = TimeSpan.FromSeconds(20),
                RetryDelay = TimeSpan.FromSeconds(5),
                ClientUserAgent = "UnitTest/1.0 (.NET CLR)",
                Logger = OutputLoggerFactory.CreateLogger<WikiClient>(),
            };
            return client;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Logout all the sites.
                var tasks = new List<Task>();
                lock (siteCache)
                {
                    foreach (var p in siteCache)
                    {
                        var site = p.Value.GetAwaiter().GetResult();
                        if (site.AccountInfo.IsUser) tasks.Add(site.LogoutAsync());
                    }
                }
                Task.WaitAll(tasks.ToArray());
            }
            base.Dispose(disposing);
        }
    }
}
