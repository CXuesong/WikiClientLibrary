using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
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
            var query = new {action = "query", meta = "siteinfo"};
            var json1 = await client.GetJsonAsync(Endpoints.WikipediaTest2,
                new WikiFormRequestMessage(query),
                CancellationToken.None);
            var json2 = await client.GetJsonAsync(Endpoints.WikipediaTest2,
                new WikiFormRequestMessage(query, true),
                CancellationToken.None);
            Output.WriteLine(json1.ToString());
        }

        [Fact]
        public async Task TestMethod2()
        {
            var client = WikiClient;
            await Assert.ThrowsAsync<InvalidActionException>(() =>
                client.GetJsonAsync(Endpoints.WikipediaTest2,
                    new WikiFormRequestMessage(new
                    {
                        action = "invalid_action_test",
                        description = "This is a test case for invalid action parameter."
                    }),
                    CancellationToken.None));
        }
    }
}
