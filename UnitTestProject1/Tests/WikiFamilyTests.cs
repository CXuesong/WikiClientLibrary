using System;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
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
            var originSite = await Family.GetSiteAsync("test2");
            // With originating WikiSite
            var link = await WikiLink.ParseAsync(originSite, Family, "WikiPedia:SANDBOX");
            AssertWikiLink(link, null, "Wikipedia", "SANDBOX");
            link = await WikiLink.ParseAsync(originSite, Family, "FR___:_ __Wp__ _:  SANDBOX");
            AssertWikiLink(link, "fr", "Wikipédia", "SANDBOX");
            link = await WikiLink.ParseAsync(originSite, Family, "EN:fr:   LZH:Project:SANDBOX");
            AssertWikiLink(link, "lzh", "維基大典", "SANDBOX");
            // We don't have de in WikiFamily, but WP has de in its inter-wiki table.
            // Should works as if we haven't specified Family.
            link = await WikiLink.ParseAsync(originSite, Family, "de:Project:SANDBOX");
            AssertWikiLink(link, "de", null, "Project:SANDBOX");
            // Without originating WikiSite
            await Assert.ThrowsAsync<ArgumentException>(() => WikiLink.ParseAsync(Family, "WikiPedia:SANDBOX"));
            link = await WikiLink.ParseAsync(Family, "FR___:_ __Wp__ _:  SANDBOX");
            AssertWikiLink(link, "fr", "Wikipédia", "SANDBOX");
            link = await WikiLink.ParseAsync(Family, "EN:fr:   LZH:Project:SANDBOX");
            AssertWikiLink(link, "lzh", "維基大典", "SANDBOX");
            await Assert.ThrowsAsync<ArgumentException>(() => WikiLink.ParseAsync(Family, "unk:WikiPedia:SANDBOX"));
        }

    }
}
