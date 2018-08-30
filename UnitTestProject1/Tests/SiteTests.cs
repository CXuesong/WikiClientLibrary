using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{

    public class SiteTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public SiteTests(ITestOutputHelper output) : base(output)
        {
        }

        private void ValidateNamespace(WikiSite site, int id, string name, bool isContent, string normalizedName = null)
        {
            Assert.True(site.Namespaces.Contains(id), $"Cannot find namespace id={id}.");
            var ns = site.Namespaces[id];
            var n = normalizedName ?? name;
            Assert.True(ns.CanonicalName == n || ns.Aliases.Contains(n));
            Assert.Equal(isContent, site.Namespaces[id].IsContent);
        }

        private void ValidateNamespaces(WikiSite site)
        {
            Assert.True(site.Namespaces.Contains(0));
            Assert.True(site.Namespaces[0].IsContent);
            ValidateNamespace(site, -2, "Media", false);
            ValidateNamespace(site, -1, "Special", false);
            ValidateNamespace(site, 1, "Talk", false);
            // btw test normalization functionality.
            ValidateNamespace(site, 1, "___ talk __", false, "Talk");
            ValidateNamespace(site, 10, "Template", false);
            ValidateNamespace(site, 11, "template_talk_", false, "Template talk");
            ValidateNamespace(site, 14, "Category", false);
        }

        [Fact]
        public async Task TestWpTest2()
        {
            var site = await WpTest2SiteAsync;
            ShallowTrace(site);
            Assert.Equal("Wikipedia", site.SiteInfo.SiteName);
            Assert.Equal("Main Page", site.SiteInfo.MainPage);
            Assert.Equal("https://test2.wikipedia.org/wiki/test%20page", site.SiteInfo.MakeArticleUrl("test page"));
            Assert.Equal("https://test2.wikipedia.org/wiki/test%20page%20(DAB)", site.SiteInfo.MakeArticleUrl("test page (DAB)"));
            var messages = await site.GetMessagesAsync(new[] {"august"});
            Assert.Equal("August", messages["august"]);
            ValidateNamespaces(site);
            //ShallowTrace(site.InterwikiMap);
            var stat = await site.GetStatisticsAsync();
            ShallowTrace(stat);
            Assert.True(stat.PagesCount > 20000); // 39244 @ 2016-08-29
            Assert.True(stat.ArticlesCount > 800); // 1145 @ 2016-08-29
            Assert.True(stat.EditsCount > 300000); // 343569 @ 2016-08-29
            Assert.True(stat.FilesCount > 50); // 126 @ 2016-08-29
            Assert.True(stat.UsersCount > 5000); // 6321 @ 2016-08-29
        }

        [Fact]
        public async Task TestWpLzh()
        {
            var site = await WpLzhSiteAsync;
            ShallowTrace(site);
            Assert.Equal("維基大典", site.SiteInfo.SiteName);
            Assert.Equal("維基大典:卷首", site.SiteInfo.MainPage);
            ValidateNamespaces(site);
            ValidateNamespace(site, BuiltInNamespaces.Project, "Wikipedia", false);
            ValidateNamespace(site, 100, "門", false);
        }

        [Fact]
        public async Task TestWikia()
        {
            var site = await WikiaTestSiteAsync;
            ShallowTrace(site);
            Assert.Equal("Mediawiki 1.19 test Wiki", site.SiteInfo.SiteName);
            ValidateNamespaces(site);
        }

        [Fact]
        public async Task LoginWpTest2_1()
        {
            var site = await WpTest2SiteAsync;
            await Assert.ThrowsAsync<OperationFailedException>(() => site.LoginAsync("user", "password"));
        }

        [Fact]
        public async Task LoginWpTest2_2()
        {
            var site = await CreateIsolatedWikiSiteAsync(Endpoints.WikipediaTest2);
            await CredentialManager.LoginAsync(site);
            Assert.True(site.AccountInfo.IsUser);
            Assert.False(site.AccountInfo.IsAnonymous);
            Output.WriteLine($"{site.AccountInfo.Name} has logged into {site}");
            await site.LogoutAsync();
            Assert.False(site.AccountInfo.IsUser);
            Assert.True(site.AccountInfo.IsAnonymous);
            Output.WriteLine($"{site.AccountInfo.Name} has logged out.");
        }

        [Fact]
        public async Task LoginWpTest2_3()
        {
            var site = new WikiSite(CreateWikiClient(),
                new SiteOptions(Endpoints.WikipediaTest2), "!!RandomUserName!!", "!!RandomPassword!!");
            await Assert.ThrowsAsync<OperationFailedException>(() => site.Initialization);
            // The initialization has failed.
            Assert.Throws<InvalidOperationException>(() => site.SiteInfo);
        }

        /// <summary>
        /// Tests legacy way for logging in. That is, to call "login" API action
        /// twice, with the second call using the token returned from the first call.
        /// </summary>
        [Fact]
        public async Task LoginWpTest2_4()
        {
            var site = await CredentialManager.EarlyLoginAsync(CreateWikiClient(),
                new SiteOptions(Endpoints.WikipediaTest2));
            ShallowTrace(site);
            await site.LogoutAsync();
        }

        /// <summary>
        /// Tests <see cref="SiteOptions.ExplicitInfoRefresh"/>.
        /// </summary>
        [SkippableFact]
        public async Task LoginPrivateWikiTest()
        {
            if (string.IsNullOrEmpty(CredentialManager.PrivateWikiTestsEntryPointUrl))
                throw new SkipException("The test needs CredentialManager.PrivateWikiTestsEntryPointUrl to be set.");
            var client = CreateWikiClient();
            // In your client code, you may load cookies beforehand,
            // and use the following statements to check whether you have already logged in
            //      var site = new WikiSite(WikiClient, "api-endpoint");
            //      await site.Initialization;
            // The second statement will throw exception if you haven't logged in.
            var site = await CredentialManager.EarlyLoginAsync(CreateWikiClient(), 
                new SiteOptions(CredentialManager.PrivateWikiTestsEntryPointUrl));
            ShallowTrace(site);
            await site.LogoutAsync();
        }

        [Fact]
        public async Task LoginWikiaTest_1()
        {
            var site = await CreateIsolatedWikiSiteAsync(Endpoints.WikiaTest);
            await CredentialManager.LoginAsync(site);
            Assert.True(site.AccountInfo.IsUser);
            Assert.False(site.AccountInfo.IsAnonymous);
            Output.WriteLine($"{site.AccountInfo.Name} has logged into {site}");
            await site.LogoutAsync();
            Assert.False(site.AccountInfo.IsUser);
            Assert.True(site.AccountInfo.IsAnonymous);
            Output.WriteLine($"{site.AccountInfo.Name} has logged out.");
        }

        [Fact]
        public async Task WpTest2OpenSearchTest()
        {
            var site = await WpTest2SiteAsync;
            var result = await site.OpenSearchAsync("San");
            ShallowTrace(result);
            Assert.True(result.Count > 0);
            Assert.Contains(result, e => e.Title == "Sandbox");
        }

        [Fact]
        public async Task WikiaOpenSearchTest()
        {
            var site = await WikiaTestSiteAsync;
            var result = await Task.WhenAll(site.OpenSearchAsync("San"),
                site.OpenSearchAsync("THIS_TITLE_DOES_NOT_EXIST"));
            ShallowTrace(result[0]);
            ShallowTrace(result[1]);
            Assert.True(result[0].Count > 0);
            Assert.Contains(result[0], e => e.Title == "Sandbox");
            Assert.True(result[1].Count == 0);
        }

        [Fact]
        public async Task SearchApiEndpointTest()
        {
            var client = CreateWikiClient();
            var result = await WikiSite.SearchApiEndpointAsync(client, "en.wikipedia.org");
            Assert.Equal("https://en.wikipedia.org/w/api.php", result);
            result = await WikiSite.SearchApiEndpointAsync(client, "mediawiki119.wikia.com");
            Assert.Equal("http://mediawiki119.wikia.com/api.php", result);
            result = await WikiSite.SearchApiEndpointAsync(client, "mediawiki119.wikia.com/abc/def");
            Assert.Equal("http://mediawiki119.wikia.com/api.php", result);
            result = await WikiSite.SearchApiEndpointAsync(client, "wikipedia.org");
            Assert.Null(result);
        }

        [Theory]
        [InlineData(Endpoints.WikipediaTest2)]
        public async Task InterlacingLoginLogoutTest(string endpointUrl)
        {
            // The two sites belong to different WikiClient instances.
            var site1 = await CreateIsolatedWikiSiteAsync(endpointUrl);
            var site2 = await CreateIsolatedWikiSiteAsync(endpointUrl);
            await CredentialManager.LoginAsync(site1);
            await CredentialManager.LoginAsync(site2);
            await site2.LogoutAsync();
            await site1.RefreshAccountInfoAsync();
            // This is a known issue of MediaWiki.
            // MediaWiki Phabricator Task T51890: Logging out on a different device logs me out everywhere else
            Assert.False(site1.AccountInfo.IsUser,
                "T51890 seems have been resolved. If this test continue to fail, please re-open the issue: https://github.com/CXuesong/WikiClientLibrary/issues/11 .");
        }

        [Fact]
        public async Task AccountAssertionTest1()
        {
            // This method will militate the Site instance…
            var site = await CreateIsolatedWikiSiteAsync(Endpoints.WikipediaTest2);
            Assert.False(site.AccountInfo.IsUser, "You should have not logged in… Wierd.");
            // Make believe that we're bots…
            typeof(AccountInfo).GetRuntimeProperty("Groups").SetValue(site.AccountInfo, new[] {"*", "user", "bot"});
            Assert.True(site.AccountInfo.IsUser, "Cannot militate user information.");
            // Send a request…
            await Assert.ThrowsAsync<AccountAssertionFailureException>(() => site.GetMessageAsync("edit"));
        }

        [Fact]
        public async Task AccountAssertionTest2()
        {
            // This method will militate the Site instance…
            var site = await CreateIsolatedWikiSiteAsync(Endpoints.WikipediaTest2);
            site.AccountAssertionFailureHandler = new MyAccountAssertionFailureHandler(async s =>
            {
                await CredentialManager.LoginAsync(site);
                return true;
            });
            Assert.False(site.AccountInfo.IsUser, "You should have not logged in… Wierd.");
            // Make believe that we're bots…
            typeof(AccountInfo).GetRuntimeProperty("Groups").SetValue(site.AccountInfo, new[] {"*", "user", "bot"});
            Assert.True(site.AccountInfo.IsUser, "Cannot militate user information.");
            // Send a request…
            var message = await site.GetMessageAsync("edit");
            Output.WriteLine("Message(edit) = " + message);
        }

        private class MyAccountAssertionFailureHandler : IAccountAssertionFailureHandler
        {
            private readonly Func<WikiSite, Task<bool>> _Handler;

            public MyAccountAssertionFailureHandler(Func<WikiSite, Task<bool>> handler)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _Handler = handler;
            }

            /// <inheritdoc />
            public Task<bool> Login(WikiSite site)
            {
                if (site == null) throw new ArgumentNullException(nameof(site));
                return _Handler(site);
            }
        }

    }
}
