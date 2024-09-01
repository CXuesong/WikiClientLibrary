using WikiClientLibrary.Client;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class WikiClientHelperTests : UnitTestsBase
{

    /// <inheritdoc />
    public WikiClientHelperTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void BuildUserAgentTest()
    {
        Assert.Equal("MyProduct", WikiClientHelper.BuildUserAgent("MyProduct"));
        Assert.Equal("MyProduct/1.1", WikiClientHelper.BuildUserAgent("MyProduct", "1.1"));
        Assert.Equal("MyProduct/1.2 (https://example.org)",
            WikiClientHelper.BuildUserAgent("MyProduct", "1.2", "https://example.org"));
        Assert.Equal("MyProduct/1.3 (https://example.org)",
            WikiClientHelper.BuildUserAgent("MyProduct", "1.3", "(https://example.org)"));
        Assert.Equal("UnitTestProject1/1.0", WikiClientHelper.BuildUserAgent(typeof(WikiClientHelperTests).Assembly));
    }

}
