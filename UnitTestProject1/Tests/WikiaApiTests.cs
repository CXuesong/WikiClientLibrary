using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Wikia;
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
        }

        [Fact]
        public async Task FetchUsersTest()
        {
            var site = await WikiaTestSiteAsync;
            var wikiaSite = new WikiaSite(site);
            var users = await wikiaSite.FetchUsersAsync(new[] {"Jasonr", "angela", "user_not_exist"}).ToArray();
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
            var user = await wikiaSite.FetchUserAsync("__mattisManzel_");
            Assert.Equal("MattisManzel", user.Name);
            Assert.Equal("MattisManzel", user.Title);
            Assert.Equal("http://mediawiki119.wikia.com/wiki/User:MattisManzel", user.UserPageUrl);
            Assert.Equal(4, user.Id);
            user = await wikiaSite.FetchUserAsync("user_not_exist");
            Assert.Null(user);
        }

        [Fact]
        public async Task FetchSiteVariablesTest()
        {
            var site = await WikiaTestSiteAsync;
            var wikiaSite = new WikiaSite(site);
            var data = await wikiaSite.FetchWikiVariablesAsync();
            ShallowTrace(data);
            Assert.Equal(203236, data.Id);
            Assert.Equal("Mediawiki 1.19 test Wiki", data.SiteName);
            Assert.Equal("http://mediawiki119.wikia.com", data.BasePath);
            Assert.Equal("/wiki/", data.ArticlePath);
            Assert.Equal(new[] {0}, data.ContentNamespaceIds);
            Assert.Equal("en", data.LanguageInfo.ContentLanguage);
            Assert.Equal("ltr", data.LanguageInfo.ContentFlowDirection);
        }

    }
}
