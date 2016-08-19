using System;
using System.Diagnostics;
using System.Globalization;
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
        private void ValidateNamespace(Site site, int id, string name, bool isContent)
        {
            Assert.IsTrue(site.Namespaces.ContainsKey(id), $"Cannot find namespace id={id}.");
            Assert.AreEqual(name, site.Namespaces[id].Name);
            Assert.AreEqual(isContent, site.Namespaces[id].IsContent);
        }

        private void ValidateNamespaces(Site site)
        {
            Assert.IsTrue(site.Namespaces.ContainsKey(0));
            Assert.IsTrue(site.Namespaces[0].IsContent);
            ValidateNamespace(site, -2, "Media", false);
            ValidateNamespace(site, -1, "Special", false);
            ValidateNamespace(site, 1, "Talk", false);
            ValidateNamespace(site, 10, "Template", false);
            ValidateNamespace(site, 14, "Category", false);
        }

        [TestMethod]
        public void TestWpTest2()
        {
            var site = CreateWikiSite(EntryPointWikipediaTest2);
            TraceSite(site);
            Assert.AreEqual("Wikipedia", site.SiteInfo.SiteName);
            Assert.AreEqual("Main Page", site.SiteInfo.MainPage);
            ValidateNamespaces(site);
        }

        [TestMethod]
        public void TestWpZh()
        {
            var site = CreateWikiSite(EntryWikipediaZh);
            TraceSite(site);
            Assert.AreEqual("Wikipedia", site.SiteInfo.SiteName);
            Assert.AreEqual("Wikipedia:首页", site.SiteInfo.MainPage);
            ValidateNamespaces(site);
        }

        [TestMethod]
        public void TestWikia()
        {
            var site = CreateWikiSite(EntryPointWikiaTest);
            TraceSite(site);
            Assert.AreEqual("Mediawiki 1.19 test Wiki", site.SiteInfo.SiteName);
            ValidateNamespaces(site);
        }

        [TestMethod]
        [ExpectedException(typeof(OperationFailedException))]
        public void LoginWpTest2_1()
        {
            var site = CreateWikiSite(EntryPointWikipediaTest2);
            AwaitSync(site.LoginAsync("user", "password"));
        }

        [TestMethod]
        public void LoginWpTest2_2()
        {
            var site = CreateWikiSite(EntryPointWikipediaTest2);
            //site.WikiClient.Timeout = TimeSpan.Zero;
            CredentialManager.Login(site);
            Assert.IsTrue(site.UserInfo.IsUser);
            Assert.IsFalse(site.UserInfo.IsAnnonymous);
            Trace.WriteLine($"{site.UserInfo.Name} has logged into {site}");
            CredentialManager.Logout(site);
            Assert.IsFalse(site.UserInfo.IsUser);
            Assert.IsTrue(site.UserInfo.IsAnnonymous);
            Trace.WriteLine($"{site.UserInfo.Name} has logged out.");
        }

    }
}
