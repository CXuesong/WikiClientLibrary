using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia;
using WikiClientLibrary.Wikia.Discussions;
using WikiClientLibrary.Wikia.WikiaApi;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{
    public class WikiaApiTests : WikiSiteTestsBase
    {
        /// <inheritdoc />
        public WikiaApiTests(ITestOutputHelper output) : base(output)
        {
            // SiteNeedsLogin(Endpoints.WikiaTest);
        }

        [Fact]
        public async Task FetchUsersTest()
        {
            var site = await WikiaTestSiteAsync;
            var users = await site.FetchUsersAsync(new[] {"Jasonr", "angela", "user_not_exist"}).ToArray();
            ShallowTrace(users);
            Assert.Equal(2, users.Length);
            Assert.Equal("Jasonr", users[0].Name);
            Assert.Equal("Jasonr", users[0].Title);
            Assert.Equal("http://mediawiki119.wikia.com/wiki/User:Jasonr", users[0].UserPageUrl);
            Assert.Equal(1, users[0].Id);
            Assert.Equal("Angela", users[1].Name);
            Assert.Equal("Angela", users[1].Title);
            Assert.Equal("http://mediawiki119.wikia.com/wiki/User:Angela", users[1].UserPageUrl);
            Assert.Equal(2, users[1].Id);
            var user = await site.FetchUserAsync("__mattisManzel_");
            Assert.Equal("MattisManzel", user.Name);
            Assert.Equal("MattisManzel", user.Title);
            Assert.Equal("http://mediawiki119.wikia.com/wiki/User:MattisManzel", user.UserPageUrl);
            Assert.Equal(4, user.Id);
            user = await site.FetchUserAsync("user_not_exist");
            Assert.Null(user);
        }

        [Fact]
        public async Task FetchSiteVariablesTest()
        {
            var site = await WikiaTestSiteAsync;
            var data = await site.FetchWikiVariablesAsync();
            ShallowTrace(data);
            Assert.Equal(203236, data.Id);
            Assert.Equal("Mediawiki 1.19 test Wiki", data.SiteName);
            Assert.Equal("http://mediawiki119.wikia.com", data.BasePath);
            Assert.Equal("/wiki/", data.ArticlePath);
            Assert.Equal(new[] {0}, data.ContentNamespaceIds);
            Assert.Equal("en", data.LanguageInfo.ContentLanguage);
            Assert.Equal("ltr", data.LanguageInfo.ContentFlowDirection);
        }

        [Fact]
        public async Task FetchRelatedPagesTest()
        {
            var site = await WikiaTestSiteAsync;
            var mainPage = new WikiPage(site, site.SiteInfo.MainPage);
            await mainPage.RefreshAsync();
            var relatedPages = await site.FetchRelatedPagesAsync(mainPage.Id);
            ShallowTrace(relatedPages);
            // These are just random titles.
            Assert.All(relatedPages, p => Assert.Matches(@"\w+\d+", p.Title));
        }

        [Fact]
        public async Task SearchListTest()
        {
            var site = await WikiaTestSiteAsync;
            var list = new LocalWikiSearchList(site, "test keyword")
            {
                PaginationSize = 10,
            };
            var results = await list.EnumItemsAsync().Take(30).ToList();
            ShallowTrace(results);
            Assert.NotEmpty(results);
            Assert.All(results, p => Assert.True(p.Quality >= list.MinimumArticleQuality));
            var exactMatches = results.Count(p =>
                p.Snippet.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0
                || p.Snippet.IndexOf("keyword", StringComparison.OrdinalIgnoreCase) >= 0
            );
            Output.WriteLine($"Exact matches: {exactMatches}/{results.Count}");
            // At least 80% of the items are exact match.
            Assert.True(exactMatches > (int)0.8 * results.Count);
        }

        [SkippableFact]
        public async Task DiscussionsTest()
        {
            var site = await WikiaTestSiteAsync;
            var page = new WikiPage(site, "1WEPN1UE18M6N");
            await page.RefreshAsync();
            Skip.IfNot(page.Exists, $"Page [[{page}]] is gone. There is nothing we can do for it.");
            // [[w:c:mediawiki119:1WEPN1UE18M6N]]
            var comments = await site.EnumArticleCommentsAsync(page.Id).ToList();
            ShallowTrace(comments);
            Assert.True(comments.Count >= 90);
        }

    }
}
