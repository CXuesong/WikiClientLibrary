using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class SiteTokenTests : WikiSiteTestsBase
    {
        /// <inheritdoc />
        public SiteTokenTests(ITestOutputHelper output) : base(output)
        {
            SiteNeedsLogin(Utility.EntryPointWikipediaTest2);
            SiteNeedsLogin(Utility.EntryPointWikiaTest);
        }

        [SkippableTheory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        public async Task TokenTest(string testSiteName)
        {
            var site = await WikiSiteFromNameAsync(testSiteName);
            foreach (var tokenType in new[] {"edit", "move", "patrol"})
            {
                // Fetch twice at a time...
                var tokenValueTask1 = site.GetTokenAsync(tokenType);
                var tokenValueTask2 = site.GetTokenAsync(tokenType);
                await Task.Delay(2000);
                string tokenValue;
                try
                {
                    tokenValue = await tokenValueTask1;
                }
                catch (Exception ex)
                {
                    if (tokenType == "patrol")
                    {
                        Output.WriteLine(ex.GetType() + ": " + ex.Message);
                        continue;
                    }
                    throw;
                }
                Assert.Matches(@"^[0-9a-f]{32,}\+\\$", tokenValue);
                // Should have the same result.
                Assert.Equal(tokenValue, await tokenValueTask2);
                // The token should have been cached.
                Assert.Equal(tokenValue, await site.GetTokenAsync(tokenType));
            }
        }

        [SkippableTheory]
        [InlineData(Utility.EntryPointWikipediaTest2, "Project:Sandbox")]
        [InlineData(Utility.EntryPointWikiaTest, "Project:Sandbox")]
        public async Task BadTokenTest(string endpointUrl, string sandboxPageTitle)
        {
            var site = await CreateIsolatedWikiSiteAsync(endpointUrl);
            var page = WikiPage.FromTitle(site, sandboxPageTitle);
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Skip.IfNot(page.Exists, $"The page {sandboxPageTitle} doesn't exist on {site}.");
            var tokensManager = typeof(WikiSite)
                .GetField("tokensManager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(site);
            var tokensCache = (IDictionary<string, object>) tokensManager.GetType()
                .GetField("tokensCache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tokensManager);
            // Place an invalid token in the cache.
            tokensCache["edit"]= @"INVALID_TOKEN+\";
            // This should cause token cache invalidation.
            await page.UpdateContentAsync("Make an empty update.", true);
            // This is a valid token
            var editToken = await site.GetTokenAsync("edit");
            Assert.Matches(@"^[0-9a-f]{32,}\+\\$", editToken);
        }

    }
}
