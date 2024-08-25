using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

/// <summary>
/// Contains tests that confirm certain issues have been resolved.
/// </summary>
public class ValidationTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
{

    public ValidationTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
    {
    }

    /// <summary>
    /// [B][Wikia]"Value was either too large or too small for an Int32."
    /// </summary>
    [Fact]
    public async Task Issue39()
    {
        // Cause: cacheBuster=7030077030012 in /api/v1/Mercury/WikiVariables request exceeds Int32 limit.
        await CreateIsolatedWikiSiteAsync("https://theedgechronicles.fandom.com/");
    }

    /// <summary>
    /// [B]ArgumentNullException in WikiPagePropertyList&lt;T&gt;
    /// </summary>
    [Fact]
    public async Task Issue67()
    {
        var site = await WpEnSiteAsync;
        var items = await new CategoriesGenerator(site)
        {
            PageTitle = MediaWikiHelper.JoinValues(new[] { "Test", ".test", "Test_(Unix)", "Test_(assessment)" }),
        }.EnumItemsAsync().ToListAsync();
        ShallowTrace(items);
    }

    /// <summary>
    /// [T]Paring/truncating Debian MediaWiki package version.
    /// </summary>
    [Fact]
    public void Issue72()
    {
        Assert.Throws<FormatException>(() => MediaWikiVersion.Parse("1.19.5-1+deb7u1"));
        var version = MediaWikiVersion.Parse("1.19.5-1+deb7u1", true);
        Assert.Equal(new MediaWikiVersion(1, 19, 5), version);
    }

    /// <summary>
    /// [B]Debian package release adds +dfsg to revision number, breaking version parsing.
    /// </summary>
    [Fact]
    public void Issue86()
    {
        Assert.Throws<FormatException>(() => MediaWikiVersion.Parse("1.19.20+dfsg-0+deb7u3"));
        var version = MediaWikiVersion.Parse("1.19.20+dfsg-0+deb7u3", true);
        Assert.Equal(new MediaWikiVersion(1, 19, 20), version);
    }

    /// <summary>
    /// [B]SiteInfo.MagicWords is not filled by anything
    /// </summary>
    [Fact]
    public async Task Issue89()
    {
        var site = await WpEnSiteAsync;
        ShallowTrace(site.MagicWords);
        Assert.NotEmpty(site.MagicWords);
        Assert.True(site.MagicWords.ContainsName("if"));
        // Magic word id is case-sensitive.
        Assert.False(site.MagicWords.ContainsName("IF"));
        Assert.True(site.MagicWords.ContainsAlias("if"));
        Assert.True(site.MagicWords.ContainsAlias("IF"));
        Assert.True(site.MagicWords.ContainsAlias("ifExpr"));
        Assert.True(site.MagicWords.ContainsAlias("__TOC__"));
        Assert.True(site.MagicWords.ContainsAlias("__toc__"));
        // Case-sensitive magic
        Assert.True(site.MagicWords.ContainsAlias("NAMESPACE"));
        Assert.False(site.MagicWords.ContainsAlias("namespace"));
        // Non-existing magic
        Assert.False(site.MagicWords.ContainsAlias("__non_existing_magic__"));
    }

}
