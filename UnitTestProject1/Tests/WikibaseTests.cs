using System;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary.Wikibase;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{
    public class WikibaseTests : WikiSiteTestsBase
    {

        private const string WikidataEntityUriPrefix = "http://www.wikidata.org/entity/";

        /// <inheritdoc />
        public WikibaseTests(ITestOutputHelper output) : base(output)
        {
        }

        private void CheckEntity(WikibaseEntity entity, string id, string labelEn)
        {
            Assert.Equal(id, entity.Id);
            var title = id;
            if (title.StartsWith("P", StringComparison.OrdinalIgnoreCase)) title = "Property:" + title;
            Assert.Equal(title, entity.Title);
            Assert.Equal(labelEn, entity.Labels["en"]);
        }

        [Fact]
        public async Task FetchEntityTest1()
        {
            var site = await WikidataSiteAsync;
            var entity = new WikibaseEntity(site, WikidataItems.Chumulangma);
            await entity.RefreshAsync(EntityQueryOptions.FetchAllProperties);
            ShallowTrace(entity);
            ShallowTrace(entity.Claims, 4);
            Assert.Equal(WikidataItems.Chumulangma, entity.Title);
            Assert.Equal(WikidataItems.Chumulangma, entity.Id);
            Assert.Equal("item", entity.Type);
            Assert.Equal("Mount Everest", entity.Labels["en"]);
            Assert.Contains("Chumulangma", entity.Aliases["en"]);
            Assert.Equal("珠穆朗玛峰", entity.Labels["zh-Hans"]);
            Assert.Equal("珠穆朗瑪峰", entity.Labels["lzh"]);
            Assert.Equal("エベレスト", entity.Labels["ja"]);

            var claim = entity.Claims[WikidataProperties.CommonsCategory].First();
            Assert.Equal(PropertyTypes.String, claim.MainSnak.DataType);
            Assert.Equal(SnakType.Value, claim.MainSnak.SnakType);
            Assert.Equal("Mount Everest", claim.MainSnak.DataValue);

            var parts = entity.Claims[WikidataProperties.PartOf].Select(c => c.MainSnak.DataValue).ToArray();
            Assert.Contains(WikidataItems.Earth, parts);
            Assert.Contains(WikidataItems.Asia, parts);

            claim = entity.Claims[WikidataProperties.CoordinateLocation].First();
            var location = (WikibaseGlobeCoordinate) claim.MainSnak.DataValue;
            Assert.Equal(27.988055555556, location.Latitude, 12);
            Assert.Equal(86.925277777778, location.Longitude, 12);
            Assert.Equal(WikidataProperties.ImportedFrom, claim.References[0].Snaks[0].PropertyId);
            Assert.Equal(WikidataItems.DeWiki, claim.References[0].Snaks[0].DataValue);

            var topiso = (WikibaseQuantity) entity.Claims[WikidataProperties.TopographicIsolation]
                .First().MainSnak.DataValue;
            Assert.Equal(40008, topiso.Amount, 12);
            Assert.Equal(WikibaseUri.Get(WikidataEntityUriPrefix + WikidataItems.Meter), topiso.Unit);
        }

        [Fact]
        public async Task FetchEntityTest2()
        {
            var site = await WikidataSiteAsync;
            var entity1 = new WikibaseEntity(site, WikidataItems.Chumulangma);
            var entity2 = new WikibaseEntity(site, WikidataItems.Chumulangma);
            var entity3 = new WikibaseEntity(site, WikidataItems.Earth);
            await new[] {entity1, entity2, entity3}.RefreshAsync(EntityQueryOptions.FetchAllProperties);
            CheckEntity(entity1, WikidataItems.Chumulangma, "Mount Everest");
            CheckEntity(entity2, WikidataItems.Chumulangma, "Mount Everest");
            CheckEntity(entity3, WikidataItems.Earth, "Earth");
        }

        public static class WikidataItems
        {

            public const string Earth = "Q2";

            public const string Asia = "Q48";

            public const string Chumulangma = "Q513";

            public const string Meter = "Q11573";

            public const string DeWiki = "Q48183";

        }

        public static class WikidataProperties
        {

            public const string ImportedFrom = "P143";

            public const string PartOf = "P361";

            public const string CommonsCategory = "P373";

            public const string CoordinateLocation = "P625";

            public const string TopographicIsolation = "P2659";

        }

    }
}
