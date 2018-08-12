using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries.Properties;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{

    public class GeneratorTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public GeneratorTests(ITestOutputHelper output) : base(output)
        {
        }

        private void AssertTitlesDistinct(IReadOnlyCollection<WikiPage> pages)
        {
            var distinctTitles = pages.Select(p => p.Title).Distinct().Count();
            Assert.Equal(pages.Count, distinctTitles);
        }

        private void TracePages(IReadOnlyCollection<WikiPage> pages)
        {
            const string lineFormat = "{0,-20} {1,10} {2,10} {3,10} {4,10}";
            Output.WriteLine(pages.Count + " pages.");
            Output.WriteLine(string.Format(lineFormat, "Title", "Length", "Last Revision", "Last Touched", "Children"));
            foreach (var page in pages)
            {
                var childrenField = "";
                var cat = page.GetPropertyGroup<CategoryInfoPropertyGroup>();
                if (cat != null)
                    childrenField = $"{cat.MembersCount}(sub:{cat.SubcategoriesCount})";
                Output.WriteLine(string.Format(lineFormat, page.Title, page.ContentLength, page.LastRevisionId,
                    page.LastTouched, childrenField));
                if (page.Content != null)
                    Output.WriteLine(page.Content.Length > 100 ? page.Content.Substring(0, 100) + "..." : page.Content);
            }
        }

        [Fact]
        public async Task WpAllPagesGeneratorTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new AllPagesGenerator(site) {PaginationSize = 500};
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpAllPagesGeneratorTest2()
        {
            var site = await WpTest2SiteAsync;
            var generator = new AllPagesGenerator(site) {StartTitle = "W", PaginationSize = 20};
            var pages = await generator.EnumPagesAsync(PageQueryOptions.FetchContent).Take(100).ToList();
            TracePages(pages);
            Assert.True(pages[0].Title[0] == 'W');
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WikiaAllPagesGeneratorTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new AllPagesGenerator(site) {NamespaceId = BuiltInNamespaces.Template, PaginationSize = 500};
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpAllCategoriesGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var generator = new AllCategoriesGenerator(site) {PaginationSize = 500};
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            generator = new AllCategoriesGenerator(site) {StartTitle = "C", PaginationSize = 20};
            pages = await generator.EnumPagesAsync(PageQueryOptions.FetchContent).Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WikiaAllCategoriesGeneratorTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new AllCategoriesGenerator(site) {PaginationSize = 500};
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpCategoryMembersGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var cat = new WikiPage(site, "Category:Template documentation pages‏‎");
            await cat.RefreshAsync();
            Output.WriteLine(cat.ToString());
            var generator = new CategoryMembersGenerator(cat) {PaginationSize = 50};
            var pages = await generator.EnumPagesAsync().ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            var catInfo = cat.GetPropertyGroup<CategoryInfoPropertyGroup>();
            Assert.Equal(catInfo.MembersCount, pages.Count);
        }

        [Fact]
        public async Task WikiaCategoryMembersGeneratorTest()
        {
            var site = await WikiaTestSiteAsync;
            var cat = new WikiPage(site, "Category:BlogListingPage‏‎‏‎");
            await cat.RefreshAsync();
            Output.WriteLine(cat.ToString());
            var generator = new CategoryMembersGenerator(cat) {PaginationSize = 50};
            var pages = await generator.EnumPagesAsync().ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            var catInfo = cat.GetPropertyGroup<CategoryInfoPropertyGroup>();
            Assert.Equal(catInfo.MembersCount, pages.Count);
        }

        [Fact]
        public async Task WpTest2RecentChangesGeneratorTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new RecentChangesGenerator(site) {LastRevisionsOnly = true, PaginationSize = 20};
            var pages = await generator.EnumPagesAsync().Take(1000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpLzhRecentChangesGeneratorTest1()
        {
            var site = await WpLzhSiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                // BotFilter = PropertyFilterOption.WithProperty,
                MinorFilter = PropertyFilterOption.WithProperty,
                AnonymousFilter = PropertyFilterOption.WithoutProperty,
                TypeFilters = RecentChangesFilterTypes.Create | RecentChangesFilterTypes.Edit,
            };
            var pages = await generator.EnumPagesAsync(PageQueryOptions.FetchContent).Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            foreach (var p in pages)
            {
                var flags = p.LastRevision.Flags;
                Assert.True(flags != RevisionFlags.None);
                Assert.False(flags.HasFlag(RevisionFlags.Anonymous));
                Assert.True(flags.HasFlag(RevisionFlags.Minor));
            }
        }

        [Fact]
        public async Task WikiaRecentChangesGeneratorTest1()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                TypeFilters = RecentChangesFilterTypes.Edit,
                PaginationSize = 500
            };
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            // Sometimes the assertion fails for wikia.
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpLzhRecentChangesListTest()
        {
            var site = await WpLzhSiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                BotFilter = PropertyFilterOption.WithProperty,
                MinorFilter = PropertyFilterOption.WithProperty,
                PaginationSize = 500
            };
            var rc = await generator.EnumItemsAsync().Take(2000).ToList();
            ShallowTrace(rc, 1);
            foreach (var p in rc)
            {
                var flags = p.Flags;
                Assert.True(flags != RevisionFlags.None);
                Assert.True(flags.HasFlag(RevisionFlags.Bot));
                Assert.True(flags.HasFlag(RevisionFlags.Minor));
            }
        }

        [Fact]
        public async Task WpLzhLogEventsListTest()
        {
            var site = await WpLzhSiteAsync;
            var generator = new LogEventsList(site)
            {
                PaginationSize = 100,
                LogType = LogTypes.Move,
                LogAction = LogActions.MoveOverRedirect,
                TimeAscending = false,
                // Local time should be converted to UTC in Utility.ToWikiQueryValue
                StartTime = DateTime.Now - TimeSpan.FromDays(7)
            };
            var logs = await generator.EnumItemsAsync().Take(200).ToList();
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

        [Fact]
        public async Task WikiaLogEventsListTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new LogEventsList(site)
            {
                PaginationSize = 100,
                LogType = LogTypes.Move,
                TimeAscending = false,
            };
            var logs = await generator.EnumItemsAsync().Take(200).ToList();
            ShallowTrace(logs, 1);
            var lastTimestamp = DateTime.MaxValue;
            foreach (var log in logs)
            {
                Assert.True(log.TimeStamp <= lastTimestamp);
                lastTimestamp = log.TimeStamp;
                Assert.Equal(LogTypes.Move, log.Type);
                Assert.Equal(LogActions.Move, log.Action);
                // Wikia doesn't have `params` node in the logevent content,
                // but LogEventsList should have taken care of it properly.
                Assert.NotNull(log.Params);
                Assert.NotNull(log.Params.TargetTitle);
                // Should not throw KeyNotFoundException
                var ns = log.Params.TargetNamespaceId;
            }
        }

        [Fact]
        public async Task WikiaRecentChangesListTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                PaginationSize = 500
            };
            var rc = await generator.EnumItemsAsync().Take(2000).ToList();
            ShallowTrace(rc, 1);
        }

        [SkippableFact]
        public async Task WpTest2PatrolTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
            };
            var rc = await generator.EnumItemsAsync().Take(2).ToList();
            Skip.If(rc.Count < 1);
            // We haven't logged in.
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() => rc[0].PatrolAsync());
        }

        [Fact]
        public async Task WpQueryPageGeneratorTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new QueryPageGenerator(site, "Ancientpages") {PaginationSize = 500};
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WikiaQueryPageGeneratorTest1()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new QueryPageGenerator(site, "Ancientpages") {PaginationSize = 500};
            var pages = await generator.EnumPagesAsync().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpGetQueryPageNamesTest()
        {
            var site = await WpTest2SiteAsync;
            var sp = await QueryPageGenerator.GetQueryPageNamesAsync(site);
            Assert.Contains("Uncategorizedpages", sp);
            ShallowTrace(sp);
        }

        [Fact]
        public async Task WpTestGetSearchTest()
        {
            var site = await WpTest2SiteAsync;
            var generator = new SearchGenerator(site, "test") {PaginationSize = 20};
            var pages = await generator.EnumPagesAsync().Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpLzhSearchTest()
        {
            var site = await WpLzhSiteAsync;
            var generator = new SearchGenerator(site, "維基") {PaginationSize = 50};
            var searchResults = await generator.EnumItemsAsync().Take(50).ToList();
            var pages = await generator.EnumPagesAsync().Take(50).ToList();
            ShallowTrace(searchResults, 1);
            TracePages(pages);
            AssertTitlesDistinct(pages);
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
                new HashSet<string>(pages.Select(p => p.Title)));
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
            var blg = new BacklinksGenerator(site, "Albert Einstein‏‎") {PaginationSize = 100};
            var pages = await blg.EnumPagesAsync().Take(100).ToList();
            ShallowTrace(pages, 1);
            Assert.Contains(pages, p => p.Title == "Judaism");
            Assert.Contains(pages, p => p.Title == "Physics");
            Assert.Contains(pages, p => p.Title == "United States");
        }

        [Fact]
        public async Task WpTranscludedInGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var tig = new TranscludedInGenerator(site, "Module:Portal‏‎") {PaginationSize = 100};
            var pages = await tig.EnumPagesAsync().Take(100).ToList();
            ShallowTrace(pages, 1);
            Assert.Contains(pages, p => p.Title == "Template:Portal bar");
        }

        [Fact]
        public async Task WpLzhEnumPageLinksTest()
        {
            var site = await WpLzhSiteAsync;
            var gen = new LinksGenerator(site, site.SiteInfo.MainPage) {PaginationSize = 20};
            Output.WriteLine(gen.PageTitle);
            var links = await gen.EnumItemsAsync().Select(stub => stub.Title).ToList();
            var linkPages = await gen.EnumPagesAsync().Select(p => p.Title).ToList();
            ShallowTrace(links);
            Assert.Contains("文言維基大典", links);
            Assert.Contains("幫助:凡例", links);
            Assert.Contains("維基大典:卓著", links);
            // The items taken from generator are unordered.
            Assert.Equal(links.ToHashSet(), linkPages.ToHashSet());
        }

        [Fact]
        public async Task WpTest2GeoSearchTest()
        {
            var site = await WpTest2SiteAsync;
            var gen = new GeoSearchGenerator(site) {TargetCoordinate = new GeoCoordinate(47.01, 2), Radius = 2000};
            var result = await gen.EnumItemsAsync().FirstOrDefault();
            ShallowTrace(result);
            Assert.NotNull(result);
            Assert.Equal("France", result.Page.Title);
            Assert.InRange(result.Distance, 1110, 1113);
            Assert.True(result.IsPrimaryCoordinate);
        }

        [Fact]
        public async Task WpCategoriesGeneratorTest()
        {
            var site = await WpLzhSiteAsync;
            var blg = new CategoriesGenerator(site, "莎拉伯恩哈特‏‎") { PaginationSize = 50 };
            var cats = await blg.EnumItemsAsync().ToList();
            ShallowTrace(cats);
            var titles = cats.Select(c => c.Title).ToList();
            Assert.Contains("分類:卓著", titles);
            Assert.Contains("分類:基礎之文", titles);
            Assert.Contains("分類:後波拿巴列傳", titles);
        }

        [Theory]
        [InlineData(Endpoints.WikipediaBetaEn, new[] {  BuiltInNamespaces.Main })]
        [InlineData(Endpoints.WikipediaBetaEn, new[] {  BuiltInNamespaces.Category })]
        [InlineData(Endpoints.WikipediaBetaEn, new[] {  BuiltInNamespaces.Project, BuiltInNamespaces.Help })]
        [InlineData(Endpoints.WikiaTest, new[] { BuiltInNamespaces.Main })]
        [InlineData(Endpoints.WikiaTest, new[] { BuiltInNamespaces.Category })]
        [InlineData(Endpoints.WikiaTest, new[] { BuiltInNamespaces.Project, BuiltInNamespaces.Help })]
        public async Task WpTest2RandomGeneratorTests(string endpoint, int[] namespaces)
        {
            var site = await GetWikiSiteAsync(endpoint);
            var generator = new RandomPageGenerator(site) { NamespaceIds = namespaces, PaginationSize = 10 };
            var stubs = await generator.EnumItemsAsync().Take(20).ToList();
            Assert.All(stubs, s => Assert.Contains(s.NamespaceId, namespaces));
            generator.RedirectsFilter = PropertyFilterOption.WithProperty;
            var pages = await generator.EnumPagesAsync().Take(20).ToList();
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
