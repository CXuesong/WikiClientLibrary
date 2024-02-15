using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using WikiClientLibrary.Tests.UnitTestProject1.Properties;
using WikiClientLibrary.Wikibase;
using WikiClientLibrary.Wikibase.DataTypes;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class WikibaseTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
    {

        private const string WikidataEntityUriPrefix = "http://www.wikidata.org/entity/";

        /// <inheritdoc />
        public WikibaseTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
        {
            SiteNeedsLogin(Endpoints.WikidataTest);
        }

        private void CheckEntity(Entity entity, string id, string labelEn)
        {
            Assert.Equal(id, entity.Id);
            var title = id;
            if (title.StartsWith("P", StringComparison.OrdinalIgnoreCase)) title = "Property:" + title;
            Assert.Equal(title, entity.Title);
            Assert.Equal(labelEn, entity.Labels["en"]);
        }

        [Fact]
        public async Task SiteInfoTest()
        {
            var site = await WikidataSiteAsync;
            var info = WikibaseSiteInfo.FromSiteInfo(site.SiteInfo);
            Assert.Equal("http://www.wikidata.org/entity/", info.ConceptBaseUri);
            Assert.Equal("https://commons.wikimedia.org/wiki/", info.GeoShapeStorageBaseUri);
            Assert.Equal("https://commons.wikimedia.org/wiki/", info.TabularDataStorageBaseUri);
            Assert.Equal("http://www.wikidata.org/entity/Q50", info.MakeEntityUri("Q50"));
            Assert.Equal("Q123", info.ParseEntityId("http://www.wikidata.org/entity/Q123"));
        }

        [Fact]
        public async Task FetchEntityTest1()
        {
            var site = await WikidataSiteAsync;
            var entity = new Entity(site, WikidataItems.Chumulangma);
            await entity.RefreshAsync(EntityQueryOptions.FetchAllProperties);
            ShallowTrace(entity);
            ShallowTrace(entity.Claims, 4);
            Assert.Equal(WikidataItems.Chumulangma, entity.Title);
            Assert.Equal(WikidataItems.Chumulangma, entity.Id);
            Assert.Equal(EntityType.Item, entity.Type);
            Assert.Equal("Mount Everest", entity.Labels["en"]);
            Assert.Contains("Qomolangma", entity.Aliases["en"]);
            Assert.Equal("珠穆朗玛峰", entity.Labels["zh-Hans"]);
            Assert.Equal("珠穆朗瑪峰", entity.Labels["lzh"]);
            Assert.Equal("エベレスト", entity.Labels["ja"]);

            var claim = entity.Claims[WikidataProperties.CommonsCategory].First();
            Assert.Equal(BuiltInDataTypes.String, claim.MainSnak.DataType);
            Assert.Equal(SnakType.Value, claim.MainSnak.SnakType);
            Assert.Equal("Mount Everest", claim.MainSnak.DataValue);

            // Now it belongs to "Seven Summits"
            //var parts = entity.Claims[WikidataProperties.PartOf].Select(c => c.MainSnak.DataValue).ToArray();
            //Assert.Contains(WikidataItems.Earth, parts);
            //Assert.Contains(WikidataItems.Asia, parts);

            claim = entity.Claims[WikidataProperties.CoordinateLocation].First();
            Utility.AssertNotNull(claim.MainSnak.DataValue);
            var location = (WbGlobeCoordinate)claim.MainSnak.DataValue;
            Assert.Equal(27.9881, location.Latitude, 3);
            Assert.Equal(86.9253, location.Longitude, 3);

            claim = entity.Claims[WikidataProperties.ElevationAboveSeaLevel].First(c => Math.Abs(((WbQuantity)c.MainSnak.DataValue!).Amount - 8848) < 0.0001);
            Utility.AssertNotNull(claim.MainSnak.DataValue);
            var height = (WbQuantity)claim.MainSnak.DataValue;
            Assert.Equal(8848, height.Amount, 4);
            Assert.Equal(WikidataProperties.ReferenceUrl, claim.References[0].Snaks[0].PropertyId);
            Assert.Equal("https://www.peakware.com/peaks.php?pk=80", claim.References[0].Snaks[0].DataValue);

            var toppro = (WbQuantity?)entity.Claims[WikidataProperties.TopographicProminence]
                .First().MainSnak.DataValue;
            Utility.AssertNotNull(toppro);
            Assert.Equal(8848.86, toppro.Value.Amount, 6);
            Assert.Equal(WikibaseUriFactory.Get(WikidataEntityUriPrefix + WikidataItems.Meter), toppro.Value.Unit);
        }

        [Fact]
        public async Task FetchEntityTest2()
        {
            var site = await WikidataSiteAsync;
            var entity1 = new Entity(site, WikidataItems.Chumulangma);
            var entity2 = new Entity(site, WikidataItems.Chumulangma);
            var entity3 = new Entity(site, WikidataItems.Earth);
            var entity4 = new Entity(site, WikidataProperties.PartOf);
            await new[] { entity1, entity2, entity3, entity4 }.RefreshAsync(EntityQueryOptions.FetchAllProperties);
            CheckEntity(entity1, WikidataItems.Chumulangma, "Mount Everest");
            CheckEntity(entity2, WikidataItems.Chumulangma, "Mount Everest");
            CheckEntity(entity3, WikidataItems.Earth, "Earth");
            CheckEntity(entity4, WikidataProperties.PartOf, "part of");
            Assert.Equal(EntityType.Property, entity4.Type);
        }


        [Fact]
        public async Task EntityFromSiteLinkTest()
        {
            var site = await WikidataSiteAsync;
            var links = new[] { "Mount Everest", "Test_(Unix)", "Inexistent title", "Earth" };
            var ids = await Entity.IdsFromSiteLinksAsync(site, "enwiki", links).ToListAsync();
            Output.WriteLine(string.Join(", ", ids));
            Assert.Equal(new[] { "Q513", "Q257491", null, "Q2" }, ids);
        }

        [SkippableTheory]
        [InlineData(EntityEditOptions.Progressive)]
        [InlineData(EntityEditOptions.Bulk)]
        public async Task EditEntityTest1(EntityEditOptions options)
        {
            AssertModify();

            const string ArbitaryItemEntityId = "Q487"; // An item ID that exists on test wiki site.
            var testSiteLink = new EntitySiteLink("testwiki", "Foo");

            options |= EntityEditOptions.Bot;

            var site = await WikidataTestSiteAsync;

            // Precondition
            var existingEntityId = await Entity.IdFromSiteLinkAsync(site, testSiteLink.Site, testSiteLink.Title);
            if (existingEntityId != null)
            {
                Output.WriteLine("Detected existing entity ID: {0}. Will perform cleanup.", existingEntityId);
                var existingEntity = new Entity(site, existingEntityId);
                await existingEntity.EditAsync(new[] { new EntityEditEntry(nameof(Entity.SiteLinks), testSiteLink, EntityEditEntryState.Removed) },
                    "Cleanup for unit test.");
                Output.WriteLine("Cleanup finished.");
            }

            var rand = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            // Create
            var entity = new Entity(site, EntityType.Item);
            var changelist = new[]
            {
                new EntityEditEntry(nameof(Entity.Labels), new WbMonolingualText("en", "test entity " + rand)),
                new EntityEditEntry(nameof(Entity.Aliases), new WbMonolingualText("en", "entity for test")),
                new EntityEditEntry(nameof(Entity.Aliases), new WbMonolingualText("en", "test")), new EntityEditEntry(nameof(Entity.Descriptions),
                    new WbMonolingualText("en",
                        "This is a test entity for unit test. If you see this entity outside the test site, please check the revision history and notify the editor.")),
                new EntityEditEntry(nameof(Entity.Descriptions), new WbMonolingualText("zh", "此实体仅用于测试之用。如果你在非测试维基见到此实体，请检查修订历史并告知编辑者。")),
                new EntityEditEntry(nameof(entity.SiteLinks), testSiteLink),
            };
            await entity.EditAsync(changelist, "Create test entity.", options);
            if ((options & EntityEditOptions.Bulk) != EntityEditOptions.Bulk)
                await entity.RefreshAsync(EntityQueryOptions.FetchLabels
                                          | EntityQueryOptions.FetchDescriptions
                                          | EntityQueryOptions.FetchAliases
                                          | EntityQueryOptions.FetchSiteLinks);
            ShallowTrace(entity);
            Assert.Equal("test entity " + rand, entity.Labels["en"]);
            Assert.Contains("test", entity.Aliases["en"]);
            Assert.Contains("This is a test entity", entity.Descriptions["en"]);
            Assert.Contains("此实体仅用于测试之用。", entity.Descriptions["zh"]);
            Assert.Equal("Foo", entity.SiteLinks["testwiki"].Title);

            // General edit
            changelist = new[]
            {
                new EntityEditEntry(nameof(Entity.Labels), new WbMonolingualText("zh-hans", "测试实体" + rand)),
                new EntityEditEntry(nameof(Entity.Labels), new WbMonolingualText("zh-hant", "測試實體" + rand)),
                // One language can have multiple aliases, so we need to specify which alias to remove, instead of using "dummy" here.
                new EntityEditEntry(nameof(Entity.Aliases), new WbMonolingualText("en", "Test"), EntityEditEntryState.Removed),
                new EntityEditEntry(nameof(Entity.Descriptions), new WbMonolingualText("zh", "dummy"), EntityEditEntryState.Removed),
                new EntityEditEntry(nameof(Entity.SiteLinks), new EntitySiteLink("testwiki", "dummy"), EntityEditEntryState.Removed),
            };
            await entity.EditAsync(changelist, "Edit test entity.", options);
            if ((options & EntityEditOptions.Bulk) != EntityEditOptions.Bulk)
                await entity.RefreshAsync(EntityQueryOptions.FetchLabels
                                          | EntityQueryOptions.FetchDescriptions
                                          | EntityQueryOptions.FetchAliases
                                          | EntityQueryOptions.FetchSiteLinks);
            ShallowTrace(entity);
            Utility.AssertNotNull(entity.Id);
            Assert.Null(entity.Descriptions["zh"]);
            Assert.Equal("测试实体" + rand, entity.Labels["zh-hans"]);
            Assert.Equal("測試實體" + rand, entity.Labels["zh-hant"]);
            Assert.DoesNotContain("Test", entity.Aliases["en"]);
            Assert.False(entity.SiteLinks.ContainsKey("testwiki"));

            // Add claim
            //  Create a property first.
            var prop = new Entity(site, EntityType.Property);
            changelist = new[]
            {
                new EntityEditEntry(nameof(Entity.Labels), new WbMonolingualText("en", "test property " + rand)),
                new EntityEditEntry(nameof(Entity.DataType), BuiltInDataTypes.WikibaseItem),
            };
            await prop.EditAsync(changelist, "Create a property for test.");
            // Refill basic information, esp. WbEntity.DataType
            await prop.RefreshAsync(EntityQueryOptions.FetchInfo);

            Utility.AssertNotNull(prop.Id);

            //  Add the claims.
            changelist = new[]
            {
                new EntityEditEntry(nameof(Entity.Claims), new Claim(new Snak(prop, entity.Id))), new EntityEditEntry(nameof(Entity.Claims),
                    new Claim(new Snak(prop, entity.Id)) { Qualifiers = { new Snak(prop, ArbitaryItemEntityId) } }),
                new EntityEditEntry(nameof(Entity.Claims),
                    new Claim(new Snak(prop, ArbitaryItemEntityId)) { References = { new ClaimReference(new Snak(prop, entity.Id)) } }),
            };
            await entity.EditAsync(changelist, "Edit test entity. Add claims.", options);
            if ((options & EntityEditOptions.Bulk) != EntityEditOptions.Bulk)
                await entity.RefreshAsync(EntityQueryOptions.FetchClaims);
            ShallowTrace(entity);

            Assert.Equal(3, entity.Claims.Count);
            Assert.Equal(3, entity.Claims[prop.Id].Count);
            Assert.Contains(entity.Claims[prop.Id], c => entity.Id.Equals(c.MainSnak.DataValue));
            var claim2 = entity.Claims[prop.Id].FirstOrDefault(c => ArbitaryItemEntityId.Equals(c.MainSnak.DataValue));
            Utility.AssertNotNull(claim2);
            Assert.Equal(entity.Id, claim2.References[0].Snaks[0].DataValue);

            //  Check consistency on the claim with qualifier
            var qualifiedClaim = entity.Claims[prop.Id].Single(c => c.Qualifiers.Count > 0);
            Assert.Equal(entity.Id, qualifiedClaim.MainSnak.DataValue);
            Assert.Single(qualifiedClaim.Qualifiers);
            Assert.Equal(prop.Id, qualifiedClaim.Qualifiers[0].PropertyId);
            Assert.Equal(ArbitaryItemEntityId, qualifiedClaim.Qualifiers[0].DataValue);

            //  Update claim qualifier
            qualifiedClaim.Qualifiers[0].DataValue = entity.Id;
            changelist = new[] { new EntityEditEntry(nameof(Entity.Claims), qualifiedClaim) };
            await entity.EditAsync(changelist, "Edit test entity. Update qualifier.", options);
            ShallowTrace(entity);

            // Remove a claim (claim2 contains the claim id now).
            changelist = new[] { new EntityEditEntry(nameof(Entity.Claims), claim2, EntityEditEntryState.Removed), };
            await entity.EditAsync(changelist, "Edit test entity. Remove a claim.", options);
            if ((options & EntityEditOptions.Bulk) != EntityEditOptions.Bulk)
                await entity.RefreshAsync(EntityQueryOptions.FetchClaims);
            Assert.Equal(2, entity.Claims.Count);
            Assert.All(entity.Claims[prop.Id], c => Assert.Equal(entity.Id, c.MainSnak.DataValue));
            Assert.Contains(entity.Claims[prop.Id], c => c.Qualifiers.Count == 0);
            Assert.Contains(entity.Claims[prop.Id], c => c.Qualifiers.Count == 1 && entity.Id.Equals(c.Qualifiers[0].DataValue));
        }

        [Fact]
        public void SerializableEntityTest()
        {
            var entity = SerializableEntity.Parse(Resources.WikibaseP3);
            Utility.AssertNotNull(entity);
            Assert.Equal("P3", entity.Id);
            Assert.Equal("instance of", entity.Labels["en"]);
            Assert.Contains(entity.Claims["P5"], c => (string?)c.MainSnak.DataValue == "Q25");
            entity = SerializableEntity.Parse(entity.ToJsonString());
            Utility.AssertNotNull(entity);
            Assert.Equal("P3", entity.Id);
            Assert.Equal("instance of", entity.Labels["en"]);
            Assert.Contains(entity.Claims["P5"], c => (string?)c.MainSnak.DataValue == "Q25");
        }


        [Fact]
        public void SerializableEntitiesTest()
        {
            var sb = new StringBuilder(Resources.WikibaseP3.Length * 2 + 20);
            sb.Append("/* */ [");
            sb.Append(Resources.WikibaseP3);
            sb.Append(", /* a */");
            sb.Append(Resources.WikibaseP3);
            sb.Append(", null");
            sb.Append("/* */ ]");
            var entities = SerializableEntity.ParseAll(sb.ToString()).ToList();
            Assert.Equal(3, entities.Count);
            Utility.AssertNotNull(entities[0]);
            Utility.AssertNotNull(entities[1]);
            Assert.Null(entities[2]);
#pragma warning disable 8602
            Assert.Equal("P3", entities[0].Id);
            Assert.Equal(entities[0].Id, entities[1].Id);
            Assert.Equal(entities[0].Labels.ToHashSet(), entities[1].Labels.ToHashSet());
            Assert.Equal(entities[0].Descriptions.ToHashSet(), entities[1].Descriptions.ToHashSet());
#pragma warning restore 8602
        }

        [Fact]
        public void SerializableEntityEofTest()
        {
            Assert.Null(SerializableEntity.Parse(""));
            Assert.Null(SerializableEntity.Parse("    \t\t    "));
            Assert.Null(SerializableEntity.Load(TextReader.Null));
            Assert.Empty(SerializableEntity.ParseAll(""));
            Assert.Empty(SerializableEntity.LoadAll(TextReader.Null));
        }

        internal static class WikidataItems
        {

            public const string Earth = "Q2";

            public const string Asia = "Q48";

            public const string Chumulangma = "Q513";

            public const string Meter = "Q11573";

            public const string KiloMeter = "Q828224";

            public const string DeWiki = "Q48183";

        }

        internal static class WikidataProperties
        {

            public const string ImportedFrom = "P143";

            public const string PartOf = "P361";

            public const string CommonsCategory = "P373";

            public const string CoordinateLocation = "P625";

            public const string ReferenceUrl = "P854";

            public const string TopographicIsolation = "P2659";

            public const string TopographicProminence = "P2660";

            public const string ElevationAboveSeaLevel = "P2044";

        }

    }
}
