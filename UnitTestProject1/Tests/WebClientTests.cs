using WikiClientLibrary.Client;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class WebClientTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
{

    /// <inheritdoc />
    public WebClientTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
    {
    }

    [Fact]
    public async Task TestMethod1()
    {
        var client = CreateWikiClient();
        var query = new {action = "query", meta = "siteinfo", format = "json"};
        var json1 = await client.InvokeAsync(Endpoints.WikipediaTest2,
            new MediaWikiFormRequestMessage(query),
            MediaWikiJsonResponseParser.Default,
            CancellationToken.None);
        var json2 = await client.InvokeAsync(Endpoints.WikipediaTest2,
            new MediaWikiFormRequestMessage(query, true),
            MediaWikiJsonResponseParser.Default,
            CancellationToken.None);
        WriteOutput(json1);
    }

    [Fact]
    public async Task TestMethod2()
    {
        var client = CreateWikiClient();
        await Assert.ThrowsAsync<InvalidActionException>(() =>
            client.InvokeAsync(Endpoints.WikipediaTest2,
                new MediaWikiFormRequestMessage(new
                {
                    action = "invalid_action_test",
                    description = "This is a test case for invalid action parameter.",
                    format = "json",
                }),
                MediaWikiJsonResponseParser.Default,
                CancellationToken.None));
    }
}