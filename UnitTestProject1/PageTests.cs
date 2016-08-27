// Enables the following conditional switch in the project options
// to prevent test cases from making any edits.
//          DRY_RUN

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class PageTests
    {
        private const string SummaryPrefix = "WikiClientLibrary test. ";

        private static readonly Lazy<Site> _WpTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikipediaTest2, true));
        private static readonly Lazy<Site> _WikiaTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikiaTest, true));
        private static readonly Lazy<Site> _WpLzhSite = new Lazy<Site>(() => CreateWikiSite(EntryWikipediaLzh));

        public static Site WpTestSite => _WpTestSite.Value;
        public static Site WikiaTestSite => _WikiaTestSite.Value;
        public static Site WpLzhSite => _WpLzhSite.Value;

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            if (_WpTestSite.IsValueCreated) CredentialManager.Logout(WpTestSite);
            if (_WikiaTestSite.IsValueCreated) CredentialManager.Logout(WikiaTestSite);
        }

        [TestMethod]
        public void WpTest2PageReadTest1()
        {
            var site = WpTestSite;
            var page = new Page(site, "project:sandbox");
            AwaitSync(page.RefreshAsync(PageQueryOptions.FetchContent));
            ShallowTrace(page);
            Assert.IsTrue(page.Exists);
            Assert.AreEqual("Wikipedia:Sandbox", page.Title);
            Assert.AreEqual(4, page.NamespaceId);
            Assert.AreEqual("en", page.PageLanguage);
            // Chars vs. Bytes
            Assert.IsTrue(page.Content.Length <= page.ContentLength);
            Trace.WriteLine(new string('-', 10));
            page = new Page(site, "file:inexistent_file.jpg");
            AwaitSync(page.RefreshAsync());
            ShallowTrace(page);
            Assert.IsFalse(page.Exists);
            Assert.AreEqual("File:Inexistent file.jpg", page.Title);
            Assert.AreEqual(6, page.NamespaceId);
            Assert.AreEqual("en", page.PageLanguage);
        }

        [TestMethod]
        public void WpTest2PageReadTest2()
        {
            var site = WpTestSite;
            var search = AwaitSync(site.OpenSearchAsync("A", 10));
            var pages = search.Select(e => new Page(site, e.Title)).ToList();
            AwaitSync(pages.RefreshAsync());
            ShallowTrace(pages);
        }

        [TestMethod]
        public void WikiaPageReadTest()
        {
            var site = WikiaTestSite;
            var page = new Page(site, "Project:Sandbox");
            AwaitSync(page.RefreshAsync(PageQueryOptions.FetchContent));
            Assert.AreEqual("Mediawiki 1.19 test Wiki:Sandbox", page.Title);
            Assert.AreEqual(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [TestMethod]
        public void WpTestEnumRevisionsTest()
        {
            var site = WpTestSite;
            // 5,100 revisions
            var page = new Page(site, "Page:Edit_page_for_chrome");
            var revisions = AwaitSync(page.EnumRevisionsAsync().Take(2000).ToList());
            ShallowTrace(revisions);
        }

        [TestMethod]
        public void WikiaEnumRevisionsTest()
        {
            var site = WikiaTestSite;
            var page = new Page(site, "Project:Sandbox");
            var revisions = AwaitSync(page.EnumRevisionsAsync().Take(2000).ToList());
            ShallowTrace(revisions);
        }

        [TestMethod]
        public void WpTestEnumPageLinksTest()
        {
            var site = WpTestSite;
            var page = new Page(site, site.SiteInfo.MainPage);
            var links = AwaitSync(page.EnumLinksAsync().ToList());
            ShallowTrace(links);
        }

        [TestMethod]
        public void WpLzhRedirectedPageReadTest()
        {
            var site = WpLzhSite;
            var page = new Page(site, "project:sandbox");
            AwaitSync(page.RefreshAsync(PageQueryOptions.ResolveRedirects));
            Assert.AreEqual("維基大典:沙盒", page.Title);
            Assert.AreEqual(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [TestMethod]
        public void WpTest2PageWriteTest1()
        {
            AssertModify();
            var site = WpTestSite;
            var page = new Page(site, "project:sandbox");
            AwaitSync(page.RefreshAsync(PageQueryOptions.FetchContent));
            page.Content += "\n\nTest from WikiClientLibrary.";
            Trace.WriteLine(page.Content);
            AwaitSync(page.UpdateContentAsync(SummaryPrefix + "Edit sandbox page."));
        }

        [TestMethod]
        [ExpectedException(typeof(UnauthorizedOperationException))]
        public void WpTest2PageWriteTest2()
        {
            AssertModify();
            var site = WpTestSite;
            var page = new Page(site, "Test page");
            AwaitSync(page.RefreshAsync(PageQueryOptions.FetchContent));
            Assert.IsTrue(page.Protections.Any(), "To perform this test, the working page should be protected.");
            page.Content += "\n\nTest from WikiClientLibrary.";
            AwaitSync(page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a protected page."));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void WpTest2PageWriteTest3()
        {
            AssertModify();
            var site = WpTestSite;
            var page = new Page(site, "Special:");
            AwaitSync(page.RefreshAsync(PageQueryOptions.FetchContent));
            page.Content += "\n\nTest from WikiClientLibrary.";
            AwaitSync(page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a special page."));
        }

        [TestMethod]
        public void WpTest2PagePurgeTest()
        {
            AssertModify();
            var site = WpTestSite;
            // We do not need to login.
            var page = new Page(site, "project:sandbox");
            var result = AwaitSync(page.PurgeAsync());
            Assert.IsTrue(result);
            // Now an ArgumentException should be thrown.
            //page = new Page(site, "special:");
            //result = AwaitSync(page.PurgeAsync());
            //Assert.IsFalse(result);
            page = new Page(site, "the page should be inexistent");
            result = AwaitSync(page.PurgeAsync());
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void WikiaPageWriteTest1()
        {
            AssertModify();
            var site = WikiaTestSite;
            AssertLoggedIn(site);
            var page = new Page(site, "project:sandbox");
            AwaitSync(page.RefreshAsync(PageQueryOptions.FetchContent));
            page.Content += "\n\nTest from WikiClientLibrary.";
            Trace.WriteLine(page.Content);
            AwaitSync(page.UpdateContentAsync(SummaryPrefix + "Edit sandbox page."));
        }
    }
}
