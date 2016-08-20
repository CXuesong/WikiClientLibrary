using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class PageTests
    {
        [TestMethod]
        public void WpTest2PageReadTest()
        {
            var site = CreateWikiSite(EntryPointWikipediaTest2);
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
            var site = CreateWikiSite(EntryPointWikiaTest);
            var page = new Page(site, "Project:Sandbox");
            AwaitSync(page.RefreshInfoAsync());
            AwaitSync(page.RefreshContentAsync());
            Assert.AreEqual("Mediawiki 1.19 test Wiki:Sandbox", page.Title);
            Assert.AreEqual(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [TestMethod]
        public void WpTest2PageWriteTest()
        {

        }
    }
}
