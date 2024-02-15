using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary.Pages.Parsing;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class RenderingTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
    {

        /// <inheritdoc />
        public RenderingTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
        {
        }

        [Fact]
        public async Task WpLzhPageParsingTest1()
        {
            var site = await WpLzhSiteAsync;
            // 一九五二年
            var result = await site.ParseRevisionAsync(240575, ParsingOptions.EffectiveLanguageLinks);
            ShallowTrace(result);
            Assert.Equal("一九五二年", result.Title);
            Assert.Matches(@"<span class=""[\w-]+"">一九五二年</span>", result.DisplayTitle);
            Assert.True(result.LanguageLinks.First(l => l.Language == "en").Title == "1952");
            Assert.True(result.LanguageLinks.First(l => l.Language == "zh").Title == "1952年");
            Assert.Contains(">公元<b>一九五二年</b>於諸曆</", result.Content);
            Assert.Contains(result.Sections, s => s.Heading == "大事");
        }

        [Fact]
        public async Task WpTestPageParsingTest1()
        {
            var site = await WpTest2SiteAsync;
            var result = await site.ParseContentAsync("{{DISPLAYTITLE:''TITLE''}}\nText '''Text'''\n\n{{PAGENAME}}", "Summary.",
                    "TITLE", ParsingOptions.DisableLimitReport);
            ShallowTrace(result, 3);
            Assert.Equal("TITLE", result.Title);
            Assert.Equal("<i>TITLE</i>", result.DisplayTitle);
            Assert.Contains("<p>Text <b>Text</b>\n</p><p>TITLE\n</p>", result.Content);
            /////////////////////
            result = await site.ParseContentAsync("{{ambox}}", "Summary.", "TITLE",
                ParsingOptions.LimitReport | ParsingOptions.TranscludedPages);
            ShallowTrace(result, 4);
            Assert.Contains(result.TranscludedPages, p => p.Title == "Template:Ambox");
            Assert.True(result.ParserLimitReports.First(r => r.Name == "limitreport-expansiondepth").Value > 1);
        }
    }
}
