using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class WikiLinkTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
{

    /// <inheritdoc />
    public WikiLinkTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
    {
    }

    [Fact]
    public async Task WikiLinkTest1()
    {
        var wpTestSite = await WpTest2SiteAsync;
        var link1 = WikiLink.Parse(wpTestSite, "____proJEct__talk_:___sandbox_");
        var link2 = WikiLink.Parse(wpTestSite, "__ _pROject_ _talk_:___sandbox_", BuiltInNamespaces.Category);
        var link3 = WikiLink.Parse(wpTestSite, "___sandbox_  test__", BuiltInNamespaces.Category);
        var link4 = WikiLink.Parse(wpTestSite, "__:   sandbox test  ", BuiltInNamespaces.Template);
        var link5 = WikiLink.Parse(wpTestSite, "___lZh__:project:test|", BuiltInNamespaces.Template);
        Assert.Equal("Wikipedia talk:Sandbox", link1.ToString());
        Assert.Equal("Wikipedia talk", link1.NamespaceName);
        Assert.Equal("Sandbox", link1.Title);
        Assert.Equal("Wikipedia talk:Sandbox", link1.Target);
        Assert.Equal("Wikipedia talk:Sandbox", link1.DisplayText);
        Assert.Equal("https://test2.wikipedia.org/wiki/Wikipedia%20talk%3ASandbox", link1.TargetUrl);
        Assert.Null(link1.InterwikiPrefix);
        Assert.Null(link1.Section);
        Assert.Null(link1.Anchor);
        Assert.Equal("Wikipedia talk:Sandbox", link2.ToString());
        Assert.Equal("Category:Sandbox test", link3.ToString());
        Assert.Equal("Sandbox test", link4.ToString());
        Assert.Equal("lzh:Project:test|", link5.ToString());
        Assert.Equal("Project:test", link5.DisplayText);
        Assert.Equal("lzh", link5.InterwikiPrefix);
        Assert.Equal("lzh:Project:test", link5.Target);
        Assert.Equal("", link5.Anchor);
        var link6 = WikiLink.Parse(wpTestSite, "sandbox#sect|anchor", BuiltInNamespaces.Template);
        Assert.Equal("Template:Sandbox#sect|anchor", link6.ToString());
        Assert.Equal("Template:Sandbox#sect", link6.Target);
        Assert.Equal("sect", link6.Section);
        Assert.Equal("anchor", link6.Anchor);
        Assert.Equal("anchor", link6.DisplayText);
    }

    [Fact]
    public async Task TestMethod2()
    {
        var wikiaTestSite = await WikiaTestSiteAsync;
        var link1 = WikiLink.Parse(wikiaTestSite, "__ _project_ _talk_:___sandbox_", BuiltInNamespaces.Category);
        var link2 = WikiLink.Parse(wikiaTestSite, "part1:part2:part3", BuiltInNamespaces.Category);
        Assert.Equal("DMan Ⅱ Wiki talk:Sandbox", link1.ToString());
        Assert.Equal("Category:Part1:part2:part3", link2.ToString());
    }

    [Fact]
    public async Task TestMethod3()
    {
        var site = await WpTest2SiteAsync;
        Assert.Throws<ArgumentException>(() => WikiLink.Parse(site, ":"));
    }

    [Fact]
    public async Task TestMethod4()
    {
        var site = await WpTest2SiteAsync;
        Assert.Throws<ArgumentException>(() => WikiLink.Parse(site, "Project:"));
    }

}
