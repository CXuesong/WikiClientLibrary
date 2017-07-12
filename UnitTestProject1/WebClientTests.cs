using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary;
using Xunit;
using Xunit.Abstractions;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
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
            var json = await client.GetJsonAsync(EntryPointWikipediaTest2,
                new {action = "query", meta = "siteinfo"},
                CancellationToken.None);
            Output.WriteLine(json.ToString());
        }

        [Fact]
        public async Task TestMethod2()
        {
            var client = WikiClient;
            await Assert.ThrowsAsync<InvalidActionException>(() =>
                client.GetJsonAsync(EntryPointWikipediaTest2,
                    new
                    {
                        action = "invalid_action_test",
                        description = "This is a test case for invalid action parameter."
                    },
                    CancellationToken.None));
        }
    }
}
