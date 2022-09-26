using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    [Trait("Category", "SiteTests")]
    public class SiteTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
    {

        /// <inheritdoc />
        public SiteTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
        {
        }

        private void ValidateNamespace(WikiSite site, int id, string name, bool isContent, string? normalizedName = null)
        {
            // normalizedName: null means using `name` as normalized name
            Assert.True(site.Namespaces.Contains(id), $"Cannot find namespace id={id}.");
            var ns = site.Namespaces[id];
            var expectedName = normalizedName ?? name;
            Assert.True(ns.CanonicalName == expectedName || ns.Aliases.Contains(expectedName));
            Assert.Equal(isContent, site.Namespaces[id].IsContent);
            // Should also be able to retrieve namespaces via name.
            Assert.True(site.Namespaces.Contains(name), $"Cannot find namespace {name} (id={id}).");
            Assert.Equal(ns, site.Namespaces[name]);
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

        private void ValidateWpInterwikiMap(WikiSite site)
        {
            // Standard names
            Assert.True(site.InterwikiMap.Contains("en"));
            Assert.True(site.InterwikiMap.Contains("zh"));
            Assert.True(site.InterwikiMap.Contains("fr"));
            Assert.Equal("English", site.InterwikiMap["en"].LanguageAutonym);
            Assert.Equal("中文", site.InterwikiMap["zh"].LanguageAutonym);
            Assert.Equal("français", site.InterwikiMap["fr"].LanguageAutonym);
            // Normalization
            Assert.True(site.InterwikiMap.Contains("EN"));
            Assert.True(site.InterwikiMap.Contains(" _zh__"));
            Assert.True(site.InterwikiMap.Contains(" FR "));
            Assert.Equal("English", site.InterwikiMap["EN"].LanguageAutonym);
            Assert.Equal("中文", site.InterwikiMap[" _zh__"].LanguageAutonym);
            Assert.Equal("français", site.InterwikiMap[" FR "].LanguageAutonym);
        }

        [Fact]
        public async Task TestWpTest2()
        {
            var site = await WpTest2SiteAsync;
            ShallowTrace(site);
            Assert.Equal("Wikipedia", site.SiteInfo.SiteName);
            Assert.Equal("Main Page", site.SiteInfo.MainPage);
            Assert.Equal("https://test2.wikipedia.org/wiki/test%20page", site.SiteInfo.MakeArticleUrl("test page"));
            Assert.Equal("https://test2.wikipedia.org/wiki/test%20page%20%28DAB%29", site.SiteInfo.MakeArticleUrl("test page (DAB)"));
            var messages = await site.GetMessagesAsync(new[] { "august" });
            Assert.Equal("August", messages["august"]);
            ValidateNamespaces(site);
            ValidateWpInterwikiMap(site);
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
            ValidateWpInterwikiMap(site);
            ValidateNamespace(site, BuiltInNamespaces.Project, "Wikipedia", false);
            ValidateNamespace(site, 100, "門", false);
        }

        [Fact]
        public async Task TestWikia()
        {
            var site = await WikiaTestSiteAsync;
            ShallowTrace(site);
            Assert.Equal("Dman Wikia", site.SiteInfo.SiteName);
            ValidateNamespaces(site);
        }

        [Theory]
        [InlineData(Endpoints.WikipediaEn)]
        [InlineData(Endpoints.WikiaTest)]
        [InlineData(Endpoints.WikipediaTest2)]
        [InlineData(Endpoints.TFWiki)]
        public async Task LoginWikiSiteFailureTest(string endpointUrl)
        {
            var site = await CreateIsolatedWikiSiteAsync(endpointUrl, true);
            var ex = await Assert.ThrowsAsync<OperationFailedException>(() => site.LoginAsync("wcl_login_failure_test", "password"));
            Output.WriteLine(ex.ToString());
        }

        [Theory]
        [InlineData(Endpoints.WikipediaEn)]
        [InlineData(Endpoints.WikiaTest)]
        [InlineData(Endpoints.WikipediaTest2)]
        [InlineData(Endpoints.TFWiki)]
        public async Task LoginWikiSiteTest(string endpointUrl)
        {
            var site = await CreateIsolatedWikiSiteAsync(endpointUrl, true);
            Assert.False(site.AccountInfo.IsUser);
            Assert.True(site.AccountInfo.IsAnonymous);

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
        public async Task LoginWpTest2_2()
        {
            var site = await CreateIsolatedWikiSiteAsync(Endpoints.WikipediaTest2, true);
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
            var site = await CredentialManager.EarlyLoginAsync(client,
                new SiteOptions(CredentialManager.PrivateWikiTestsEntryPointUrl));
            ShallowTrace(site);
            await site.LogoutAsync();
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
            var result = await Task.WhenAll(site.OpenSearchAsync("Dman Wi"),
                site.OpenSearchAsync("THISTITLEDOESNOTEXIST"));
            ShallowTrace(result[0]);
            ShallowTrace(result[1]);
            Assert.True(result[0].Count > 0);
            Assert.Contains(result[0], e => e.Title == "Dman Wikia");
            Assert.True(result[1].Count == 0);
        }

        [Fact]
        public async Task SearchApiEndpointTest()
        {
            var client = CreateWikiClient();
            var result = await WikiSite.SearchApiEndpointAsync(client, "en.wikipedia.org");
            Assert.Equal("https://en.wikipedia.org/w/api.php", result);
            result = await WikiSite.SearchApiEndpointAsync(client, "warriors.fandom.com");
            Assert.Equal("https://warriors.fandom.com/api.php", result);
            result = await WikiSite.SearchApiEndpointAsync(client, "warriors.fandom.com/abc/def");
            Assert.Equal("https://warriors.fandom.com/api.php", result);
            result = await WikiSite.SearchApiEndpointAsync(client, "wikipedia.org");
            Assert.Null(result);
        }

        [Theory]
        [InlineData(Endpoints.WikipediaEn)]
        [InlineData(Endpoints.WikiaTest)]
        [InlineData(Endpoints.WikipediaTest2)]
        [InlineData(Endpoints.TFWiki)]
        public async Task InterlacingLoginLogoutTest(string endpointUrl)
        {
            // The two sites belong to different WikiClient instances.
            var site1 = await CreateIsolatedWikiSiteAsync(endpointUrl, true);
            var site2 = await CreateIsolatedWikiSiteAsync(endpointUrl, true);
            Assert.False(site1.AccountInfo.IsUser);
            Assert.False(site1.AccountInfo.IsUser);
            await CredentialManager.LoginAsync(site1);
            await CredentialManager.LoginAsync(site2);
            await site2.LogoutAsync();
            await site1.RefreshAccountInfoAsync();
            // MediaWiki Phabricator Task T51890: Logging out on a different device logs me out everywhere else
            Assert.True(site1.AccountInfo.IsUser,
                "If you are logged in with your normal password instead of Bot Password, " +
                "This case will fail due to [[phab:T51890]] and you can safely ignore it." +
                "If you are logged in with Bot Password, please re-open the issue: " +
                "https://github.com/CXuesong/WikiClientLibrary/issues/11 .");
        }

        [Theory]
        [InlineData(Endpoints.WikipediaEn)]
        [InlineData(Endpoints.WikiaTest)]
        [InlineData(Endpoints.WikipediaTest2)]
        // [InlineData(Endpoints.TFWiki)] account assertion does not work on MW 1.19.
        public async Task AccountAssertionTest1(string endpointUrl)
        {
            // This method will fiddle with the Site instance…
            var site = await CreateIsolatedWikiSiteAsync(endpointUrl, true);
            Assert.False(site.AccountInfo.IsUser, "You should have not logged in.");
            // Make believe that we're bots…
            typeof(AccountInfo).GetProperty(nameof(AccountInfo.Groups))!.SetValue(site.AccountInfo, new[] { "*", "user", "bot" });
            Assert.True(site.AccountInfo.IsUser, "Failed to fiddle with user information.");
            // Send a request…
            await Assert.ThrowsAsync<AccountAssertionFailureException>(() => site.GetMessageAsync("edit"));
        }

        [Theory]
        [InlineData(Endpoints.WikipediaEn)]
        [InlineData(Endpoints.WikiaTest)]
        [InlineData(Endpoints.WikipediaTest2)]
        [InlineData(Endpoints.TFWiki)]
        public async Task AccountAssertionTest2(string endpointUrl)
        {
            // This method will fiddle with the Site instance…
            var site = await CreateIsolatedWikiSiteAsync(endpointUrl, true);
            site.AccountAssertionFailureHandler = new MyAccountAssertionFailureHandler(async s =>
            {
                await CredentialManager.LoginAsync(site);
                return true;
            });
            Assert.False(site.AccountInfo.IsUser, "You should have not logged in.");
            // Make believe that we're bots…
            typeof(AccountInfo).GetProperty(nameof(AccountInfo.Groups))!.SetValue(site.AccountInfo, new[] { "*", "user", "bot" });
            Assert.True(site.AccountInfo.IsUser, "Failed to fiddle with user information.");
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
