using System.Reflection;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class SiteTokenTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
{

    /// <inheritdoc />
    public SiteTokenTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
    {
        SiteNeedsLogin(Endpoints.WikipediaTest2);
        SiteNeedsLogin(Endpoints.WikiaTest);
        SiteNeedsLogin(Endpoints.TFWiki);
    }

    [SkippableTheory]
    [InlineData(nameof(WpTest2SiteAsync))]
    [InlineData(nameof(WikiaTestSiteAsync))]
    [InlineData(nameof(TFWikiSiteAsync))]
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

    [Theory]
    [InlineData(Endpoints.WikipediaTest2, "Project:Sandbox")]
    [InlineData(Endpoints.WikiaTest, "Project:Sandbox")]
    [InlineData(Endpoints.TFWiki, "User:FuncGammaBot/Sandbox")]
    public async Task BadTokenTest(string endpointUrl, string sandboxPageTitle)
    {
        const string invalidToken = @"INVALID_TOKEN+\";
        var site = await CreateIsolatedWikiSiteAsync(endpointUrl);
        var page = new WikiPage(site, sandboxPageTitle);
        await page.RefreshAsync(PageQueryOptions.FetchContent);
        Assert.True(page.Exists, $"The page {sandboxPageTitle} doesn't exist on {site}.");
        var tokensManager = typeof(WikiSite)
            .GetField("tokensManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(site);
        var tokensCache = (IDictionary<string, object>)tokensManager!.GetType()
            .GetField("tokensCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tokensManager)!;
        // Place an invalid token in the cache.
        tokensCache["edit"] = invalidToken;
        tokensCache["csrf"] = invalidToken;
        Assert.Equal(invalidToken, await site.GetTokenAsync("edit"));
        try
        {
            // This should cause token cache invalidation.
            await page.EditAsync(new WikiPageEditOptions { Content = page.Content!, Summary = "Make an empty update.", Minor = true });
        }
        catch (OperationFailedException ex) when (ex.ErrorCode == "globalblocking-blockedtext-range")
        {
            // wikimedia-globalblocking-ipblocked-range
            Output.WriteLine("UpdateContentAsync fails due to IP block: " + ex);
            // However, this does not affect our retrieving a new valid token.
            // Continue testing.
        }
        // This is a valid token
        var editToken = await site.GetTokenAsync("edit");
        Assert.Matches(@"^[0-9a-f]{32,}\+\\$", editToken);
    }

}
