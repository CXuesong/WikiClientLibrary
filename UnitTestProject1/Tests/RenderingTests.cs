using WikiClientLibrary.Pages.Parsing;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

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

        WriteOutput("Parsed revision");
        ShallowTrace(result);

        Assert.Equal("一九五二年", result.Title);
        Assert.Matches(@"<span class=""[\w-]+"">一九五二年</span>", result.DisplayTitle);
        Assert.True(result.LanguageLinks.First(l => l.Language == "en").Title == "1952");
        Assert.True(result.LanguageLinks.First(l => l.Language == "zh").Title == "1952年");
        Assert.Contains(">公元<b>一九五二年</b>於諸曆</", result.Content);

        WriteOutput("Sections");
        ShallowTrace(result.Sections);

        Assert.Equal(3, result.Sections.Count);

        Assert.Equal("1", result.Sections[0].Index);
        Assert.Equal("一", result.Sections[0].Number);
        Assert.Equal(11, result.Sections[0].ByteOffset);
        Assert.Equal(2, result.Sections[0].Level);
        Assert.Equal(1, result.Sections[0].TocLevel);
        Assert.Equal("大事", result.Sections[0].Heading);
        Assert.Equal("大事", result.Sections[0].Anchor);
        Assert.Equal("一九五二年", result.Sections[0].PageTitle);

        Assert.Equal("2", result.Sections[1].Index);
        Assert.Equal("生", result.Sections[1].Heading);

        Assert.Equal("3", result.Sections[2].Index);
        Assert.Equal("卒", result.Sections[2].Heading);
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
