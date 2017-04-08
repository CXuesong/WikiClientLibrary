using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class WikiFamilyTests
    {

        private readonly Lazy<Family> _Family;

        private readonly Lazy<Site> _WpTest2Site;

        private Family Family => _Family.Value;

        private Site WpTest2Site => _WpTest2Site.Value;

        private string GetWPEntrypoint(string prefix)
        {
            return "https://" + prefix + ".wikipedia.org/w/api.php";
        }

        public WikiFamilyTests()
        {
            _Family = new Lazy<Family>(()=>
            {
                var f = new Family(CreateWikiClient(), "Wikipedia");
                f.Logger = DefaultTraceLogger;
                f.Register("en", GetWPEntrypoint("en"));
                f.Register("fr", GetWPEntrypoint("fr"));
                f.Register("test2", GetWPEntrypoint("test2"));
                f.Register("lzh", GetWPEntrypoint("zh-classical"));
                return f;
            });
            _WpTest2Site = new Lazy<Site>(() => AwaitSync(Family.GetSiteAsync("test2")));
        }

        private void AssertWikiLink(WikiLink link, string interwiki, string ns, string localTitle)
        {
            Assert.AreEqual(interwiki, link.InterwikiPrefix);
            Assert.AreEqual(ns, link.NamespaceName);
            Assert.AreEqual(localTitle, link.Title);
        }

        [TestMethod]
        public void InterwikiLinkTests()
        {
            // We will not login onto any site…
            var link = AwaitSync(WikiLink.ParseAsync(WpTest2Site, Family, "WikiPedia:SANDBOX"));
            AssertWikiLink(link, null, "Wikipedia", "SANDBOX");
            link = AwaitSync(WikiLink.ParseAsync(WpTest2Site, Family, "FR___:_ __Wp__ _:  SANDBOX"));
            AssertWikiLink(link, "fr", "Wikipédia", "SANDBOX");
            link = AwaitSync(WikiLink.ParseAsync(WpTest2Site, Family, "EN:fr:   LZH:Project:SANDBOX"));
            AssertWikiLink(link, "lzh", "維基大典", "SANDBOX");
        }
    }
}
