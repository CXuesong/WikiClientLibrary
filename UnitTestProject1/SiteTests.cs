using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class SiteTests
    {
        private static Site WpTestSite;
        private static Site WikiaTestSite;

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {
            // Prepare test environment.
            WpTestSite = CreateWikiSite(EntryPointWikipediaTest2);
            //CredentialManager.Login(WpTestSite);
            WikiaTestSite = CreateWikiSite(EntryPointWikiaTest);
            //CredentialManager.Login(WikiaTestSite);
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
            var site = CreateWikiSite(EntryWikipediaLzh);
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
        [ExpectedException(typeof(OperationFailedException))]
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
            Assert.IsTrue(site.UserInfo.IsUser);
            Assert.IsFalse(site.UserInfo.IsAnnonymous);
            Trace.WriteLine($"{site.UserInfo.Name} has logged into {site}");
            CredentialManager.Logout(site);
            Assert.IsFalse(site.UserInfo.IsUser);
            Assert.IsTrue(site.UserInfo.IsAnnonymous);
            Trace.WriteLine($"{site.UserInfo.Name} has logged out.");
        }


        [TestMethod]
        public void LoginWikiaTest_1()
        {
            var site = WikiaTestSite;
            CredentialManager.Login(site);
            Assert.IsTrue(site.UserInfo.IsUser);
            Assert.IsFalse(site.UserInfo.IsAnnonymous);
            Trace.WriteLine($"{site.UserInfo.Name} has logged into {site}");
            CredentialManager.Logout(site);
            Assert.IsFalse(site.UserInfo.IsUser);
            Assert.IsTrue(site.UserInfo.IsAnnonymous);
            Trace.WriteLine($"{site.UserInfo.Name} has logged out.");
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
    }
}
