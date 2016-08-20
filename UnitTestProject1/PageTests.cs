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

        private static Site WpTestSite;

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {
            // Prepare test environment.
            WpTestSite = CreateWikiSite(EntryPointWikipediaTest2);
            CredentialManager.Login(WpTestSite);
        }

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            CredentialManager.Logout(WpTestSite);
        }


        [TestMethod]
        public void WpTest2PageReadTest()
        {
            var site = WpTestSite;
            var page = new Page(site, "project:sandbox");
            AwaitSync(page.RefreshContentAsync());
            ShallowTrace(page);
            Assert.IsTrue(page.Exists);
            Assert.AreEqual("Wikipedia:Sandbox", page.Title);
            Assert.AreEqual(4, page.NamespaceId);
            Assert.AreEqual("en", page.PageLanguage);
            // Chars vs. Bytes
            Assert.IsTrue(page.Content.Length <= page.ContentLength);
            Trace.WriteLine(new string('-', 10));
            page = new Page(site, "file:inexistent_file.jpg");
            AwaitSync(page.RefreshInfoAsync());
            ShallowTrace(page);
            Assert.IsFalse(page.Exists);
            Assert.AreEqual("File:Inexistent file.jpg", page.Title);
            Assert.AreEqual(6, page.NamespaceId);
            Assert.AreEqual("en", page.PageLanguage);
        }

        [TestMethod]
        public void WikiaPageReadTest()
        {
            var site = WpTestSite;
            var page = new Page(site, "Project:Sandbox");
            AwaitSync(page.RefreshInfoAsync());
            AwaitSync(page.RefreshContentAsync());
            Assert.AreEqual("Mediawiki 1.19 test Wiki:Sandbox", page.Title);
            Assert.AreEqual(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [TestMethod]
        public void WpTest2PageWriteTest1()
        {
            AssertModify();
            var site = WpTestSite;
            var page = new Page(site, "project:sandbox");
            AwaitSync(page.RefreshContentAsync());
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
            AwaitSync(page.RefreshContentAsync());
            Assert.IsTrue(page.Protections.Any(), "To perform this test, the working page should be protected.");
            page.Content += "\n\nTest from WikiClientLibrary.";
            AwaitSync(page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a protected page."));
        }

        [TestMethod]
        [ExpectedException(typeof(OperationFailedException))]
        public void WpTest2PageWriteTest3()
        {
            AssertModify();
            var site = WpTestSite;
            var page = new Page(site, "Special:");
            AwaitSync(page.RefreshContentAsync());
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
            page = new Page(site, "special:");
            result = AwaitSync(page.PurgeAsync());
            Assert.IsFalse(result);
            page = new Page(site, "the page should be inexistent");
            result = AwaitSync(page.PurgeAsync());
            Assert.IsFalse(result);
        }
    }
}
