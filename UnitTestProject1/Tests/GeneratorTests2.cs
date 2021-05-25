using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;
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



        // The test will not hold since ruwarriorswiki has upgraded into MW 1.33
        // [Fact]
        internal async Task WikiaLogEventsListLoopTest()
        {
            // This is a known scenario. There are a lot of logs on ruwarriorswiki with the same timestamp.
            var site = await GetWikiSiteAsync(Endpoints.RuWarriorsWiki);
            var generator = new LogEventsList(site)
            {
                PaginationSize = 50,
                LogType = LogTypes.Move,
                StartTime = DateTime.Parse("2019-09-28T07:31:07Z", CultureInfo.InvariantCulture),
                EndTime = DateTime.Parse("2019-10-03T15:29:10Z", CultureInfo.InvariantCulture),
                TimeAscending = true,
            };
            // Take only first page, fine.
            var logs = await generator.EnumItemsAsync().Take(50).ToListAsync();
            Output.WriteLine("{0}", logs.FirstOrDefault());
            Output.WriteLine("{0}", logs.LastOrDefault());
            Assert.Equal("FANDOMbot", logs.First().UserName);
            Assert.Equal("FANDOMbot", logs.Last().UserName);
            // Take the second page, it throws.
            await Assert.ThrowsAsync<UnexpectedDataException>(() => generator.EnumItemsAsync().Take(51).ToListAsync().AsTask());
            // Introduce some last-resorts.
            generator.CompatibilityOptions = new WikiListCompatibilityOptions
            {
                ContinuationLoopBehaviors = WikiListContinuationLoopBehaviors.FetchMore
            };
            var logs2 = await generator.EnumItemsAsync().Take(100).ToListAsync();
            Output.WriteLine("logs = {0}", string.Join(",", logs.Select(l => l.LogId)));
            Output.WriteLine("logs2 = {0}", string.Join(",", logs2.Select(l => l.LogId)));
            // The first 50 items should be the same.
            Assert.Equal(logs.Select(l => l.LogId), logs2.Take(50).Select(l => l.LogId));
            // The next 50 items should not be duplicate with the first 100 items.
            var logs2Id = logs2.Skip(50).Select(l => l.LogId).ToHashSet();
            foreach (var l in logs)
            {
                Assert.DoesNotContain(l.LogId, logs2Id);
            }
        }


        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task RecentChangesGeneratorTest1(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new RecentChangesGenerator(site) { LastRevisionsOnly = true, PaginationSize = 20 };
            var pages = await generator.EnumPagesAsync().Take(200).ToListAsync();
            TracePages(pages);
            Utility.AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task RecentChangesGeneratorTest2(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                // BotFilter = PropertyFilterOption.WithProperty,
                MinorFilter = PropertyFilterOption.WithProperty,
                AnonymousFilter = PropertyFilterOption.WithoutProperty,
                TypeFilters = RecentChangesFilterTypes.Create | RecentChangesFilterTypes.Edit,
            };
            var pages = await generator.EnumPagesAsync(PageQueryOptions.FetchContent).Take(100).ToListAsync();
            TracePages(pages);
            Utility.AssertTitlesDistinct(pages);
            foreach (var p in pages)
            {
                var flags = p.LastRevision!.Flags;
                Assert.True(flags != RevisionFlags.None);
                Assert.False(flags.HasFlag(RevisionFlags.Anonymous));
                Assert.True(flags.HasFlag(RevisionFlags.Minor));
            }
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task RecentChangesGeneratorTest3(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                TypeFilters = RecentChangesFilterTypes.Edit,
                PaginationSize = 500
            };
            var pages = await generator.EnumPagesAsync().Take(2000).ToListAsync();
            TracePages(pages);
            // Sometimes the assertion fails for wikia.
            Utility.AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task RecentChangesListTest(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                PaginationSize = 500
            };
            var rc = await generator.EnumItemsAsync().Take(2000).ToListAsync();
            ShallowTrace(rc, 1);
        }

        [SkippableFact]
        [CISkipped(Reason = CISkippedReason.AgentBlocked)]
        public async Task WpTest2PatrolTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                PatrolledFilter = PropertyFilterOption.WithoutProperty
            };
            var rc = await generator.EnumItemsAsync().Take(2).ToListAsync();
            Output.WriteLine("Changes to patrol:");
            ShallowTrace(rc);
            Skip.If(rc.Count < 1);
            // We require the user has patrol permission on WpTest2 site.
            await rc[0].PatrolAsync();
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

        [Theory]
        [InlineData("File:This.png", "User:Pbm/gallery")] // image
        [InlineData("File:Ae Fond Kiss local.ogg", "User:JanGerber/sandbox")] // audio
        public async Task WpFileUsageGeneratorTest(string target, string expected)
        {
            var site = await WpTest2SiteAsync;
            var fug = new FileUsageGenerator(site, target) { PaginationSize = 100 };
            var pages = await fug.EnumPagesAsync().Take(100).ToListAsync();
            ShallowTrace(pages, 1);
            Assert.Contains(pages, p => p.Title == expected);
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

        [Fact]
        public async Task WpTestGetSearchTest()
        {
            var site = await WpTest2SiteAsync;
            var generator = new SearchGenerator(site, "test") { PaginationSize = 20 };
            var pages = await generator.EnumPagesAsync().Take(200).ToListAsync();
            TracePages(pages);
            Utility.AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpLzhSearchTest()
        {
            var site = await WpLzhSiteAsync;
            var generator = new SearchGenerator(site, "維基") { PaginationSize = 50 };
            var searchResults = await generator.EnumItemsAsync().Take(50).ToListAsync();
            var pages = await generator.EnumPagesAsync().Take(50).ToListAsync();
            ShallowTrace(searchResults, 1);
            TracePages(pages);
            Utility.AssertTitlesDistinct(pages);
            // Note as 2017-03-07, [[維基]] actually exists on lzh wiki, but it's a redirect to [[維基媒體基金會]].
            // Maybe that's why it's not included in the search result.
            //Assert.True(pages.Any(p => p.Title == "維基"));
            Assert.Contains(pages, p => p.Title == "維基媒體基金會");
            Assert.Contains(pages, p => p.Title == "維基大典");
            Assert.Contains(pages, p => p.Title == "文言維基大典");
            Assert.All(searchResults, r => Assert.Contains(generator.Keyword, r.Snippet));
            // Note there might be pages in the list with equal score, in which case, some adjacent items
            // in the result might have different order between each request.
            // We will take care of the situation. Just ensure we have most of the desired items.
            Assert.ProperSuperset(new HashSet<string>(searchResults.Select(r => r.Title).Take(40)),
                new HashSet<string>(pages.Select(p => p.Title!)));
        }

    }
}
