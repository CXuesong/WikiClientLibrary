using System;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{

    public class WikiFamilyTests : WikiSiteTestsBase
    {

        private readonly Lazy<WikiFamily> _Family;

        private WikiFamily Family => _Family.Value;

        /// <inheritdoc />
        public WikiFamilyTests(ITestOutputHelper output) : base(output)
        {
            _Family = new Lazy<WikiFamily>(() =>
            {
                var f = new WikiFamily(CreateWikiClient(), "Wikipedia");
                f.Logger = new TestOutputLogger(output);
                f.Register("en", GetWPEntrypoint("en"));
                f.Register("fr", GetWPEntrypoint("fr"));
                f.Register("test2", GetWPEntrypoint("test2"));
                f.Register("lzh", GetWPEntrypoint("zh-classical"));
                return f;
            });
        }

        private string GetWPEntrypoint(string prefix)
        {
            return "https://" + prefix + ".wikipedia.org/w/api.php";
        }

        private void AssertWikiLink(WikiLink link, string interwiki, string ns, string localTitle)
        {
            Assert.Equal(interwiki, link.InterwikiPrefix);
            Assert.Equal(ns, link.NamespaceName);
            Assert.Equal(localTitle, link.Title);
        }

        [Fact]
        public async Task InterwikiLinkTests()
        {
            // We will not login onto any site…
            var homeSite = await Family.GetSiteAsync("test2");
            var link = await WikiLink.ParseAsync(homeSite, Family, "WikiPedia:SANDBOX");
            AssertWikiLink(link, null, "Wikipedia", "SANDBOX");
            link = await WikiLink.ParseAsync(homeSite, Family, "FR___:_ __Wp__ _:  SANDBOX");
            AssertWikiLink(link, "fr", "Wikipédia", "SANDBOX");
            link = await WikiLink.ParseAsync(homeSite, Family, "EN:fr:   LZH:Project:SANDBOX");
            AssertWikiLink(link, "lzh", "維基大典", "SANDBOX");
        }

    }
}
