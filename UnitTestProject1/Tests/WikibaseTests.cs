using System.Threading.Tasks;
using WikiClientLibrary.Wikibase;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{
    public class WikibaseTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public WikibaseTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task FetchEntityTest()
        {
            var site = await WikidataSiteAsync;
            var entity = new WikibaseEntity(site, "Q513");
            await entity.RefreshAsync(EntityQueryOptions.FetchAllProperties);
            ShallowTrace(entity);
            ShallowTrace(entity.Claims, 4);
            Assert.Equal("Q513", entity.Title);
            Assert.Equal("item", entity.Type);
            Assert.Equal("Mount Everest", entity.Labels["en"]);
            Assert.Contains("Chumulangma", entity.Aliases["en"]);
            Assert.Equal("珠穆朗玛峰", entity.Labels["zh-Hans"]);
            Assert.Equal("珠穆朗瑪峰", entity.Labels["lzh"]);
            Assert.Equal("エベレスト", entity.Labels["ja"]);
        }

    }
}
