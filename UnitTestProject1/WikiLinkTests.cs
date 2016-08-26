using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class WikiLinkTests
    {
        private static readonly Lazy<Site> _WpTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikipediaTest2));
        private static readonly Lazy<Site> _WikiaTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikiaTest));

        public static Site WpTestSite => _WpTestSite.Value;
        public static Site WikiaTestSite => _WikiaTestSite.Value;

        [TestMethod]
        public void TestMethod1()
        {
            var link1 = new WikiLink(WpTestSite, "____project__talk_:___sandbox_");
            var link2 = new WikiLink(WpTestSite, "__ _project_ _talk_:___sandbox_", BuiltInNamespaces.Category);
            var link3 = new WikiLink(WpTestSite, "___sandbox_  test__", BuiltInNamespaces.Category);
            var link4 = new WikiLink(WpTestSite, "__:   sandbox test  ", BuiltInNamespaces.Template);
            var link5 = new WikiLink(WpTestSite, "___lZh__:project:test", BuiltInNamespaces.Template);
            Assert.AreEqual("Wikipedia talk:Sandbox", link1.ToString());
            Assert.AreEqual("Wikipedia talk", link1.NamespaceName);
            Assert.AreEqual("Sandbox", link1.Title);
            Assert.AreEqual(null, link1.InterwikiPrefix);
            Assert.AreEqual(null, link1.Section);
            Assert.AreEqual(null, link1.Anchor);
            Assert.AreEqual("Wikipedia talk:Sandbox", link2.ToString());
            Assert.AreEqual("Category:Sandbox test", link3.ToString());
            Assert.AreEqual("Sandbox test", link4.ToString());
            Assert.AreEqual("lzh:Project:test", link5.ToString());
            Assert.AreEqual("lzh", link5.InterwikiPrefix);
            var link6 = new WikiLink(WpTestSite, "sandbox#sect|anchor", BuiltInNamespaces.Template);
            Assert.AreEqual("Template:Sandbox#sect|anchor", link6.ToString());
            Assert.AreEqual("sect", link6.Section);
            Assert.AreEqual("anchor", link6.Anchor);
        }

        [TestMethod]
        public void TestMethod2()
        {
            var link1 = new WikiLink(WikiaTestSite, "__ _project_ _talk_:___sandbox_", BuiltInNamespaces.Category);
            var link2= new WikiLink(WikiaTestSite, "part1:part2:part3", BuiltInNamespaces.Category);
            Assert.AreEqual("Mediawiki 1.19 test Wiki talk:Sandbox", link1.ToString());
            Assert.AreEqual("Category:Part1:part2:part3", link2.ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestMethod3()
        {
            var link = new WikiLink(WpTestSite, ":");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestMethod4()
        {
            var link = new WikiLink(WpTestSite, "Project:");
        }
    }
}
