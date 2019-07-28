using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class WebClientTests : WikiSiteTestsBase
    {
        /// <inheritdoc />
        public WebClientTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestMethod1()
        {
            var client = WikiClient;
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
            var client = WikiClient;
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
}
