using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class GeneratorTests2 : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
    {

        /// <inheritdoc />
        public GeneratorTests2(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
        {
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        // [InlineData(nameof(TFWikiSiteAsync))]        // there is no move/move_redirect leaction on TFWiki.
        public async Task LogEventsListTest1(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new LogEventsList(site)
            {
                PaginationSize = 100,
                LogType = LogActions.Move,
                LogAction = LogActions.MoveOverRedirect,
                TimeAscending = false,
                // Local time should be converted to UTC in Utility.ToWikiQueryValue
                StartTime = DateTime.Now - TimeSpan.FromDays(7)
            };
            var logs = await generator.EnumItemsAsync().Take(200).ToListAsync();
            ShallowTrace(logs, 1);
            var lastTimestamp = generator.StartTime.Value;
            foreach (var log in logs)
            {
                Assert.True(log.TimeStamp <= lastTimestamp);
                lastTimestamp = log.TimeStamp;
                Assert.Equal(LogTypes.Move, log.Type);
                Assert.Equal(LogActions.MoveOverRedirect, log.Action);
            }
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task LogEventsListTest2(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var startTime = DateTime.UtcNow - TimeSpan.FromDays(30);
            var generator = new LogEventsList(site)
            {
                PaginationSize = 100,
                LogType = LogTypes.Move,
                StartTime = startTime,
                TimeAscending = false,
            };
            var logs = await generator.EnumItemsAsync().Take(200).ToListAsync();
            ShallowTrace(logs, 1);
            var lastTimestamp = DateTime.MaxValue;
            Assert.All(logs, log =>
            {
                Assert.True(log.TimeStamp <= startTime);
                Assert.True(log.TimeStamp <= lastTimestamp);
                lastTimestamp = log.TimeStamp;
                Assert.Equal(LogTypes.Move, log.Type);
                Assert.Contains(log.Action, new[] { LogActions.Move, LogActions.MoveOverRedirect });

                // Wikia doesn't have `params` node in the logevent content,
                // but LogEventsList should have taken care of it properly.
                Utility.AssertNotNull(log.Params);

                if ((log.HiddenFields & LogEventHiddenFields.Action) != LogEventHiddenFields.Action)
                {
                    Utility.AssertNotNull(log.Params.TargetTitle);
                    // Should not throw KeyNotFoundException
                    _ = log.Params.TargetNamespaceId;
                }

                if ((log.HiddenFields & LogEventHiddenFields.User) != LogEventHiddenFields.User)
                {
                    Assert.NotEqual(0, log.UserId);
                    Utility.AssertNotNull(log.UserName);
                }
            });
        }


        [Fact]
        public async Task WikiaGetQueryPageNamesTest()
        {
            var site = await WikiaTestSiteAsync;
            var sp = await QueryPageGenerator.GetQueryPageNamesAsync(site);
            ShallowTrace(sp);
            Assert.Contains("Uncategorizedpages", sp);
        }

        [Fact]
        public async Task WpBackLinksGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var blg = new BacklinksGenerator(site, "Albert Einstein‏‎") { PaginationSize = 100 };
            var pages = await blg.EnumPagesAsync().Take(100).ToListAsync();
            ShallowTrace(pages, 1);
            Assert.Contains(pages, p => p.Title == "Judaism");
            Assert.Contains(pages, p => p.Title == "Physics");
            Assert.Contains(pages, p => p.Title == "United States");
        }

        [Fact]
        public async Task WpTranscludedInGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var tig = new TranscludedInGenerator(site, "Module:Portal‏‎") { PaginationSize = 100 };
            var pages = await tig.EnumPagesAsync().Take(100).ToListAsync();
            ShallowTrace(pages, 1);
            Assert.Contains(pages, p => p.Title == "Template:Portal bar");
        }

        [Fact]
        public async Task WpLzhEnumPageLinksTest()
        {
            var site = await WpLzhSiteAsync;
            var gen = new LinksGenerator(site, site.SiteInfo.MainPage) { PaginationSize = 20 };
            Output.WriteLine(gen.PageTitle);
            var links = await gen.EnumItemsAsync().Select(stub => stub.Title).ToListAsync();
            var linkPages = await gen.EnumPagesAsync().Select(p => p.Title).ToListAsync();
            ShallowTrace(links);
            Assert.Contains("文言維基大典", links);
            Assert.Contains("幫助:凡例", links);
            Assert.Contains("維基大典:卓著", links);
            // The items taken from generator are unordered.
            Assert.Equal(links.ToHashSet(), linkPages.ToHashSet());
        }

        [Fact]
        public async Task WpEnGeoSearchTest1()
        {
            var site = await WpEnSiteAsync;
            var gen = new GeoSearchGenerator(site) { TargetCoordinate = new GeoCoordinate(47.01, 2), Radius = 2000 };
            var result = await gen.EnumItemsAsync().Take(10).FirstOrDefaultAsync(r => r.Page.Title == "France");
            ShallowTrace(result);
            Utility.AssertNotNull(result);
            Assert.InRange(result.Distance, 1110, 1113);
            Assert.True(result.IsPrimaryCoordinate);
        }

        [Fact]
        public async Task WpEnGeoSearchTest2()
        {
            var site = await WpEnSiteAsync;
            var gen = new GeoSearchGenerator(site) { BoundingRectangle = new GeoCoordinateRectangle(1.9, 47.1, 0.2, 0.2) };
            var result = await gen.EnumItemsAsync().Take(20).FirstOrDefaultAsync(r => r.Page.Title == "France");
            ShallowTrace(result);
            Utility.AssertNotNull(result);
            Assert.True(result.IsPrimaryCoordinate);
        }

        [Fact]
        public async Task WpCategoriesGeneratorTest()
        {
            var site = await WpLzhSiteAsync;
            var blg = new CategoriesGenerator(site, "莎拉伯恩哈特‏‎") { PaginationSize = 50 };
            var cats = await blg.EnumItemsAsync().ToListAsync();
            ShallowTrace(cats);
            var titles = cats.Select(c => c.Title).ToList();
            Assert.Contains("分類:基礎之文", titles);
            Assert.Contains("分類:後波拿巴列傳", titles);
        }

        [Theory]
        [InlineData(nameof(WpBetaSiteAsync), new[] { BuiltInNamespaces.Main })]
        [InlineData(nameof(WpBetaSiteAsync), new[] { BuiltInNamespaces.Category })]
        [InlineData(nameof(WpBetaSiteAsync), new[] { BuiltInNamespaces.Project, BuiltInNamespaces.Help })]
        [InlineData(nameof(WikiaTestSiteAsync), new[] { BuiltInNamespaces.Main })]
        [InlineData(nameof(WikiaTestSiteAsync), new[] { BuiltInNamespaces.Category })]
        [InlineData(nameof(WikiaTestSiteAsync), new[] { BuiltInNamespaces.Project, BuiltInNamespaces.Help })]
        [InlineData(nameof(TFWikiSiteAsync), new[] { BuiltInNamespaces.Main })]
        [InlineData(nameof(TFWikiSiteAsync), new[] { BuiltInNamespaces.Category })]
        [InlineData(nameof(TFWikiSiteAsync), new[] { BuiltInNamespaces.Project, BuiltInNamespaces.Help })]
        public async Task RandomGeneratorTest(string siteName, int[] namespaces)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new RandomPageGenerator(site) { NamespaceIds = namespaces, PaginationSize = 10 };
            var stubs = await generator.EnumItemsAsync().Take(20).ToListAsync();
            Assert.All(stubs, s => Assert.Contains(s.NamespaceId, namespaces));
            generator.RedirectsFilter = PropertyFilterOption.WithProperty;
            var pages = await generator.EnumPagesAsync().Take(20).ToListAsync();
            Assert.All(pages, p =>
            {
                Assert.Contains(p.NamespaceId, namespaces);
                Assert.True(p.IsRedirect);
            });
            // We are obtaining random sequence.
            if (stubs.Count > 0) Assert.NotEqual(stubs.Select(s => s.Title), pages.Select(p => p.Title));
        }

    }
}
