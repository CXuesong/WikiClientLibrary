using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class SiteTests
    {
        private static readonly Lazy<Site> _WpTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikipediaTest2));
        private static readonly Lazy<Site> _WikiaTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikiaTest));
        private static readonly Lazy<Site> _WpLzhSite = new Lazy<Site>(() => CreateWikiSite(EntryWikipediaLzh));

        public static Site WpTestSite => _WpTestSite.Value;
        public static Site WikiaTestSite => _WikiaTestSite.Value;
        public static Site WpLzhSite => _WpLzhSite.Value;

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {

        }

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            //CredentialManager.Logout(WpTestSite);
        }

        private void ValidateNamespace(Site site, int id, string name, bool isContent, string normalizedName = null)
        {
            Assert.IsTrue(site.Namespaces.Contains(id), $"Cannot find namespace id={id}.");
            var ns = site.Namespaces[id];
            var n = normalizedName ?? name;
            Assert.IsTrue(ns.CanonicalName == n || ns.Aliases.Contains(n));
            Assert.AreEqual(isContent, site.Namespaces[id].IsContent);
        }

        private void ValidateNamespaces(Site site)
        {
            Assert.IsTrue(site.Namespaces.Contains(0));
            Assert.IsTrue(site.Namespaces[0].IsContent);
            ValidateNamespace(site, -2, "Media", false);
            ValidateNamespace(site, -1, "Special", false);
            ValidateNamespace(site, 1, "Talk", false);
            // btw test normalization functionality.
            ValidateNamespace(site, 1, "___ talk __", false, "Talk");
            ValidateNamespace(site, 10, "Template", false);
            ValidateNamespace(site, 11, "template_talk_", false, "Template talk");
            ValidateNamespace(site, 14, "Category", false);
        }

        [TestMethod]
        public void TestWpTest2()
        {
            var site = WpTestSite;
            ShallowTrace(site);
            Assert.AreEqual("Wikipedia", site.SiteInfo.SiteName);
            Assert.AreEqual("Main Page", site.SiteInfo.MainPage);
            var messages = AwaitSync(site.GetMessagesAsync(new[] {"august"}));
            Assert.AreEqual("August", messages["august"]);
            ValidateNamespaces(site);
            //ShallowTrace(site.InterwikiMap);
            var stat = AwaitSync(site.GetStatisticsAsync());
            ShallowTrace(stat);
            Assert.IsTrue(stat.PagesCount > 20000); // 39244 @ 2016-08-29
            Assert.IsTrue(stat.ArticlesCount > 800); // 1145 @ 2016-08-29
            Assert.IsTrue(stat.EditsCount > 300000); // 343569 @ 2016-08-29
            Assert.IsTrue(stat.FilesCount > 50); // 126 @ 2016-08-29
            Assert.IsTrue(stat.UsersCount > 5000); // 6321 @ 2016-08-29
        }

        [TestMethod]
        public void TestWpLzh()
        {
            var site = WpLzhSite;
            ShallowTrace(site);
            Assert.AreEqual("維基大典", site.SiteInfo.SiteName);
            Assert.AreEqual("維基大典:卷首", site.SiteInfo.MainPage);
            ValidateNamespaces(site);
            ValidateNamespace(site, BuiltInNamespaces.Project, "Wikipedia", false);
            ValidateNamespace(site, 100, "門", false);
        }

        [TestMethod]
        public void TestWikia()
        {
            var site = WikiaTestSite;
            ShallowTrace(site);
            Assert.AreEqual("Mediawiki 1.19 test Wiki", site.SiteInfo.SiteName);
            ValidateNamespaces(site);
        }

        [TestMethod]
        [ExpectedException(typeof (OperationFailedException))]
        public void LoginWpTest2_1()
        {
            var site = WpTestSite;
            AwaitSync(site.LoginAsync("user", "password"));
        }

        [TestMethod]
        public void LoginWpTest2_2()
        {
            var site = WpTestSite;
            CredentialManager.Login(site);
            Assert.IsTrue(site.AccountInfo.IsUser);
            Assert.IsFalse(site.AccountInfo.IsAnonymous);
            Trace.WriteLine($"{site.AccountInfo.Name} has logged into {site}");
            CredentialManager.Logout(site);
            Assert.IsFalse(site.AccountInfo.IsUser);
            Assert.IsTrue(site.AccountInfo.IsAnonymous);
            Trace.WriteLine($"{site.AccountInfo.Name} has logged out.");
        }

        /// <summary>
        /// Tests <see cref="SiteOptions.ExplicitInfoRefresh"/>.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof (InvalidOperationException))]
        public void LoginWpTest2_3()
        {
            var site = AwaitSync(Site.CreateAsync(CreateWikiClient(),
                new SiteOptions(EntryPointWikipediaTest2) {ExplicitInfoRefresh = true}));
            var x = site.SiteInfo.Version;
        }


        /// <summary>
        /// Tests legacy way for logging in. That is, to call "login" API action
        /// twice, with the second call using the token returned from the first call.
        /// </summary>
        [TestMethod]
        public void LoginWpTest2_4()
        {
            var site = AwaitSync(Site.CreateAsync(CreateWikiClient(),
                new SiteOptions(EntryPointWikipediaTest2) {ExplicitInfoRefresh = true}));
            CredentialManager.Login(site);
            AwaitSync(site.RefreshSiteInfoAsync());
            ShallowTrace(site);
            CredentialManager.Logout(site);
        }

        /// <summary>
        /// Tests <see cref="SiteOptions.ExplicitInfoRefresh"/>.
        /// </summary>
        [TestMethod]
        public void LoginPrivateWikiTest()
        {
            if (string.IsNullOrEmpty(CredentialManager.PrivateWikiTestsEntryPointUrl))
                Assert.Inconclusive("The test needs CredentialManager.PrivateWikiTestsEntryPointUrl to be set.");
            var client = CreateWikiClient();
            // Load cookies, if any. Here we just create a client from scratch.
            var site = AwaitSync(Site.CreateAsync(client,
                new SiteOptions(CredentialManager.PrivateWikiTestsEntryPointUrl)
                {
                    ExplicitInfoRefresh = true
                }));
            bool needsLogin;
            try
            {
                // It's better to get user (rather than site) info here.
                AwaitSync(site.RefreshAccountInfoAsync());
                // If the attempt is succcessful, it means we should have logged in.
                // After all, it's a private wiki, where anonymous users shouldn't have
                // access to reading the wiki.
                needsLogin = !site.AccountInfo.IsUser;
                // If needsLogin evaluates to true here... Well, you'd better
                // check if your private wiki is private enough.
                // Nonetheless, the code still works XD
            }
            catch (UnauthorizedOperationException)
            {
                // Cannot read user info. We must haven't logged in.
                needsLogin = true;
            }
            if (needsLogin)
            {
                // Login if needed.
                CredentialManager.Login(site);
                Debug.Assert(site.AccountInfo.IsUser);
            }
            // Login succeeded. We should initialize site information.
            AwaitSync(site.RefreshSiteInfoAsync());
            // Now we can do something.
            ShallowTrace(site);
            CredentialManager.Logout(site);
        }

        [TestMethod]
        public void LoginWikiaTest_1()
        {
            var site = WikiaTestSite;
            CredentialManager.Login(site);
            Assert.IsTrue(site.AccountInfo.IsUser);
            Assert.IsFalse(site.AccountInfo.IsAnonymous);
            Trace.WriteLine($"{site.AccountInfo.Name} has logged into {site}");
            CredentialManager.Logout(site);
            Assert.IsFalse(site.AccountInfo.IsUser);
            Assert.IsTrue(site.AccountInfo.IsAnonymous);
            Trace.WriteLine($"{site.AccountInfo.Name} has logged out.");
        }

        [TestMethod]
        public void WpTest2OpenSearchTest()
        {
            var site = WpTestSite;
            var result = AwaitSync(site.OpenSearchAsync("San"));
            ShallowTrace(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.Any(e => e.Title == "Sandbox"));
        }

        [TestMethod]
        public void WikiaOpenSearchTest()
        {
            var site = WikiaTestSite;
            var result = AwaitSync(Task.WhenAll(site.OpenSearchAsync("San"),
                site.OpenSearchAsync("THIS_TITLE_DOES_NOT_EXIST")));
            ShallowTrace(result[0]);
            ShallowTrace(result[1]);
            Assert.IsTrue(result[0].Count > 0);
            Assert.IsTrue(result[0].Any(e => e.Title == "Sandbox"));
            Assert.IsTrue(result[1].Count == 0);
        }

        [TestMethod]
        public void SearchApiEndpointTest()
        {
            var client = CreateWikiClient();
            var result = AwaitSync(Site.SearchApiEndpointAsync(client, "en.wikipedia.org"));
            Assert.AreEqual("https://en.wikipedia.org/w/api.php", result);
            result = AwaitSync(Site.SearchApiEndpointAsync(client, "mediawiki119.wikia.com"));
            Assert.AreEqual("http://mediawiki119.wikia.com/api.php", result);
            result = AwaitSync(Site.SearchApiEndpointAsync(client, "mediawiki119.wikia.com/abc/def"));
            Assert.AreEqual("http://mediawiki119.wikia.com/api.php", result);
            result = AwaitSync(Site.SearchApiEndpointAsync(client, "wikipedia.org"));
            Assert.AreEqual(null, result);
        }

        [TestMethod]
        [ExpectedException(typeof(AccountAssertionFailureException))]
        public void AccountAssertionTest1()
        {
            // This method will militate the Site instance…
            var site = CreateWikiSite(EntryPointWikipediaTest2);
            Assert.IsFalse(site.AccountInfo.IsUser, "You should have not logged in… Wierd.");
            // Make believe that we're bots…
            typeof(AccountInfo).GetProperty("Groups").SetValue(site.AccountInfo, new[] {"*", "user", "bot"});
            Assert.IsTrue(site.AccountInfo.IsUser, "Cannot militate user information.");
            // Send a request…
            var message = AwaitSync(site.GetMessageAsync("edit"));
        }

        [TestMethod]
        public void AccountAssertionTest2()
        {
            // This method will militate the Site instance…
            var site = CreateWikiSite(EntryPointWikipediaTest2);
            site.AccountAssertionFailureHandler = new MyAccountAssertionFailureHandler(s =>
            {
                CredentialManager.Login(site);
                return Task.FromResult(true);
            });
            Assert.IsFalse(site.AccountInfo.IsUser, "You should have not logged in… Wierd.");
            // Make believe that we're bots…
            typeof(AccountInfo).GetProperty("Groups").SetValue(site.AccountInfo, new[] {"*", "user", "bot"});
            Assert.IsTrue(site.AccountInfo.IsUser, "Cannot militate user information.");
            // Send a request…
            var message = AwaitSync(site.GetMessageAsync("edit"));
            Trace.WriteLine("Message(edit) = " + message);
        }

        private class MyAccountAssertionFailureHandler : IAccountAssertionFailureHandler
        {
            private readonly Func<Site, Task<bool>> _Handler;

            public MyAccountAssertionFailureHandler(Func<Site, Task<bool>> handler)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _Handler = handler;
            }

            /// <inheritdoc />
            public Task<bool> Login(Site site)
            {
                if (site == null) throw new ArgumentNullException(nameof(site));
                return _Handler(site);
            }
        }
    }
}
