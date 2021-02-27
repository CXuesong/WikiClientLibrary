using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    // Our IP of CI is blocked from editing by WP and blocked from login by Wikia. Sad story.
    // By using Bot Password, we may bypass this issue.
    public class SiteTokenTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public SiteTokenTests(ITestOutputHelper output) : base(output)
        {
            SiteNeedsLogin(Endpoints.WikipediaTest2);
            SiteNeedsLogin(Endpoints.WikiaTest);
        }

        [SkippableTheory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        public async Task TokenTest(string testSiteName)
        {
            var site = await WikiSiteFromNameAsync(testSiteName);
            foreach (var tokenType in new[] { "edit", "move", "patrol" })
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
            // Invalid tokens
            await Assert.ThrowsAsync<ArgumentException>(() => site.GetTokenAsync("invalid_token_type"));
        }

        [SkippableTheory]
        [InlineData(Endpoints.WikipediaTest2, "Project:Sandbox")]
        [InlineData(Endpoints.WikiaTest, "Project:Sandbox")]
        public async Task BadTokenTest(string endpointUrl, string sandboxPageTitle)
        {
            const string invalidToken = @"INVALID_TOKEN+\";
            var site = await CreateIsolatedWikiSiteAsync(endpointUrl);
            var page = new WikiPage(site, sandboxPageTitle);
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Skip.IfNot(page.Exists, $"The page {sandboxPageTitle} doesn't exist on {site}.");
            var tokensManager = typeof(WikiSite)
                .GetField("tokensManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(site);
            var tokensCache = (IDictionary<string, object>)tokensManager!.GetType()
                .GetField("tokensCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tokensManager)!;
            // Place an invalid token in the cache.
            tokensCache["edit"] = invalidToken;
            tokensCache["csrf"] = invalidToken;
            Assert.Equal(invalidToken, await site.GetTokenAsync("edit"));
            // This should cause token cache invalidation.
            await page.UpdateContentAsync("Make an empty update.", true);
            // This is a valid token
            var editToken = await site.GetTokenAsync("edit");
            Assert.Matches(@"^[0-9a-f]{32,}\+\\$", editToken);
        }

    }

}
