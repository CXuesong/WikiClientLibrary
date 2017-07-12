using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Dictionary<string, Task<Site>> siteCache = new Dictionary<string, Task<Site>>();

        protected void SiteNeedsLogin(string endpointUrl)
        {
            sitesNeedsLogin.Add(endpointUrl);
        }

        protected Task<Site> GetWikiSiteAsync(string endpointUrl)
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

        protected Task<Site> CreateIsolatedWikiSiteAsync(string apiEndpoint)
        {
            var isolatedClient = CreateWikiClient();
            return CreateWikiSiteAsync(isolatedClient, apiEndpoint);
        }

        private async Task<Site> CreateWikiSiteAsync(WikiClientBase wikiClient, string url)
        {
            var options = new SiteOptions(url)
            {
                AccountAssertion = AccountAssertionBehavior.AssertAll
            };
            var site = await Site.CreateAsync(wikiClient, options);
            site.Logger = new TestOutputLogger(Output);
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
            Utility.Inconclusive("Remove #define DRY_RUN to perform edit tests.");
#endif
        }

        protected Task<Site> WpTest2SiteAsync => GetWikiSiteAsync(Utility.EntryPointWikipediaTest2);

        protected Task<Site> WikiaTestSiteAsync => GetWikiSiteAsync(Utility.EntryPointWikiaTest);

        protected Task<Site> WpLzhSiteAsync => GetWikiSiteAsync(Utility.EntryWikipediaLzh);

        protected Task<Site> WpBetaSiteAsync => GetWikiSiteAsync(Utility.EntryPointWikipediaBetaEn);

        protected WikiClient CreateWikiClient()
        {
            var client = new WikiClient
            {
                Logger = new TestOutputLogger(Output),
                Timeout = TimeSpan.FromSeconds(20),
                RetryDelay = TimeSpan.FromSeconds(5),
                ClientUserAgent = "UnitTest/1.0 (.NET CLR)",
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
