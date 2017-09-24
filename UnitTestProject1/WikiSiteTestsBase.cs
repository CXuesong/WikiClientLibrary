// Enables the following conditional switch here
// to prevent test cases from making any edits.

//#define DRY_RUN

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
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
        }

        public ITestOutputHelper Output { get; }

        protected void ShallowTrace(object obj, int depth = 2)
        {
            Output.WriteLine(Utility.DumpObject(obj, depth));
        }

        /// <inheritdoc />
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

        protected Task<WikiSite> CreateIsolatedWikiSiteAsync(string apiEndpoint)
        {
            var isolatedClient = CreateWikiClient();
            return CreateWikiSiteAsync(isolatedClient, apiEndpoint);
        }

        private async Task<WikiSite> CreateWikiSiteAsync(WikiClientBase wikiClient, string url)
        {
            var options = new SiteOptions(url)
            {
                AccountAssertion = AccountAssertionBehavior.AssertAll
            };
            var site = await WikiSite.CreateAsync(wikiClient, options);
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

        protected Task<WikiSite> WpTest2SiteAsync => GetWikiSiteAsync(Utility.EntryPointWikipediaTest2);

        protected Task<WikiSite> WikiaTestSiteAsync => GetWikiSiteAsync(Utility.EntryPointWikiaTest);

        protected Task<WikiSite> WpLzhSiteAsync => GetWikiSiteAsync(Utility.EntryWikipediaLzh);

        protected Task<WikiSite> WpBetaSiteAsync => GetWikiSiteAsync(Utility.EntryPointWikipediaBetaEn);

        protected Task<WikiSite> WikimediaCommonsBetaSiteAsync => GetWikiSiteAsync(Utility.EntryPointWikimediaCommonsBeta);

        protected WikiClient CreateWikiClient()
        {
            var lf = new LoggerFactory();
            lf.AddProvider(new TestOutputLoggerProvider(Output));
            var client = new WikiClient
            {
                Timeout = TimeSpan.FromSeconds(20),
                RetryDelay = TimeSpan.FromSeconds(5),
                ClientUserAgent = "UnitTest/1.0 (.NET CLR)",
                LoggerFactory = lf,
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
