using WikiClientLibrary.Pages;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class PageHelperTests : UnitTestsBase
{

    /// <inheritdoc />
    public PageHelperTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("Test (disambiguation)", "Test")]
    [InlineData("Test(assessment)", "Test")]
    [InlineData("Test (assessment", "Test")]
    [InlineData("Test  \t (disambiguation)", "Test")]
    [InlineData("政治学 （亚里士多德）", "政治学")]
    [InlineData("政治学 (アリストテレス)", "政治学")]
    public void StripTitleDisambiguationTest(string originalTitle, string strippedTitle)
    {
        Assert.Equal(strippedTitle, PageHelper.StripTitleDisambiguation(originalTitle));
    }

    [Theory]
    [InlineData("\n\n\r\ntest\n  \r  \n", "\n\n\ntest")]
    [InlineData("{{User sandbox}}\r\n<!-- EDIT BELOW THIS LINE -->\r\n\r\ntest123123\n\n\n\n",
        "{{User sandbox}}\n<!-- EDIT BELOW THIS LINE -->\n\ntest123123")]
    public void SanitizePageContentTest(string content, string sanitizedContent)
    {
        Assert.Equal(sanitizedContent, PageHelper.SanitizePageContent(content));
    }

    [Theory]
    [InlineData("test123", "7288edd0fc3ffcbe93a0cf06e3568e28521687bc")]
    [InlineData("test\n123", "5fd30ebeba53fc4614bcf3b00c4c55b9cd70b266")]
    [InlineData("{{User sandbox}}\r\n<!-- EDIT BELOW THIS LINE -->\r\n\r\ntest123123\n\n\n\n", "46f9909a6eb8dac9a24e3e4b85bf2a89e6983c1e")]
    public void EvaluateSanitizedSha1Test(string content, string sha1)
    {
        Assert.Equal(sha1, PageHelper.EvaluateSanitizedSha1(content));
    }

}
