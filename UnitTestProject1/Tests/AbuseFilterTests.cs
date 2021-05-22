using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.AbuseFilters;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class AbuseFilterTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
    {

        /// <inheritdoc />
        public AbuseFilterTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
        {
        }

        [Fact]
        public async Task AbuseFilterListTest()
        {
            var site = await WpTest2SiteAsync;
            var aflist = new AbuseFilterList(site) {PaginationSize = 30};
            var items = await aflist.EnumItemsAsync().ToListAsync();
            ShallowTrace(items);
            Assert.Contains(items, f => f.LastEditor == "Luke081515");
        }

    }
}
