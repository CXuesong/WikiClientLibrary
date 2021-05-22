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

    public class GeneratorTests1 : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
    {

        /// <inheritdoc />
        public GeneratorTests1(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
        {
        }

        private void AssertTitlesDistinct(IList<WikiPage> pages)
        {
            var distinctTitles = pages.GroupBy(p => p.Title);
            foreach (var group in distinctTitles)
            {
                var count = group.Count();
                var indices = string.Join(',', group.Select(pages.IndexOf));
                Assert.True(count == 1, $"Page instance [[{group.Key}]] is not unique. Found {count} instances with indices {indices}.");
            }
        }

        private void TracePages(IReadOnlyCollection<WikiPage> pages)
        {
            const string lineFormat = "{0,-20} {1,10} {2,10} {3,10} {4,10}";
#if ENV_CI_BUILD
            const int ITEMS_LIMIT = 10;
#else
            const int ITEMS_LIMIT = int.MaxValue;
#endif
            WriteOutput("{0} pages.", pages.Count);
            WriteOutput(lineFormat, "Title", "Length", "Last Revision", "Last Touched", "Children");
            foreach (var page in pages.Take(ITEMS_LIMIT))
            {
                var childrenField = "";
                var cat = page.GetPropertyGroup<CategoryInfoPropertyGroup>();
                if (cat != null)
                    childrenField = $"{cat.MembersCount}(sub:{cat.SubcategoriesCount})";
                WriteOutput(lineFormat, page.Title, page.ContentLength, page.LastRevisionId, page.LastTouched, childrenField);
                if (page.Content != null)
                    WriteOutput(page.Content.Length > 100 ? page.Content.Substring(0, 100) + "..." : page.Content);
            }
            if (pages.Count > ITEMS_LIMIT)
            {
                WriteOutput("[+{0} pages]", pages.Count - ITEMS_LIMIT);
            }
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task AllPagesGeneratorTest1(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new AllPagesGenerator(site) { PaginationSize = 500 };
            var pages = await generator.EnumPagesAsync().Take(2000).ToListAsync();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task AllPagesGeneratorTest2(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new AllPagesGenerator(site) { StartTitle = "W", PaginationSize = 20 };
            var pages = await generator.EnumPagesAsync(PageQueryOptions.FetchContent).Take(100).ToListAsync();
            TracePages(pages);
            Assert.StartsWith("W", pages[0].Title!);
            AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task AllPagesGeneratorTest3(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new AllPagesGenerator(site) { NamespaceId = BuiltInNamespaces.Template, PaginationSize = 500 };
            var pages = await generator.EnumPagesAsync().Take(2000).ToListAsync();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync), 4)]
        [InlineData(nameof(WikiaTestSiteAsync), 4)]
        [InlineData(nameof(TFWikiSiteAsync), 2)] // query page on TFWiki is slow.
        public async Task AllCategoriesGeneratorTest1(string siteName, int batches)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new AllCategoriesGenerator(site) { PaginationSize = 200 };
            var pages = await generator.EnumPagesAsync().Take(200 * batches).ToListAsync();
            TracePages(pages);
            generator = new AllCategoriesGenerator(site) { StartTitle = "C", PaginationSize = 200 };
            pages = await generator.EnumPagesAsync(PageQueryOptions.FetchContent).Take(200 * batches).ToListAsync();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync), "Category:Template documentation pages‏‎")]
        [InlineData(nameof(WikiaTestSiteAsync), "BlogListingPage‏‎‏‎")]
        [InlineData(nameof(TFWikiSiteAsync), "Category:Autobot subgroups‏‎")]
        public async Task CategoryMembersGeneratorTest(string siteName, string categoryName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var cat = new WikiPage(site, categoryName, BuiltInNamespaces.Category);
            await cat.RefreshAsync();
            WriteOutput(cat);
            var generator = new CategoryMembersGenerator(cat) { PaginationSize = 50 };
            var pages = await generator.EnumPagesAsync().ToListAsync();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            var catInfo = cat.GetPropertyGroup<CategoryInfoPropertyGroup>();
            Utility.AssertNotNull(catInfo);
            Assert.Equal(catInfo.MembersCount, pages.Count);
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
            AssertTitlesDistinct(pages);
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
            AssertTitlesDistinct(pages);
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
            AssertTitlesDistinct(pages);
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
        [CISkipped(Reason=CISkippedReason.AgentBlocked)]
        public async Task WpTest2PatrolTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new RecentChangesGenerator(site) { 
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
        [InlineData(nameof(WpTest2SiteAsync), 4)]
        [InlineData(nameof(WikiaTestSiteAsync), 4)]
        [InlineData(nameof(TFWikiSiteAsync), 2)] // query page on TFWiki is slow.
        public async Task QueryPageGeneratorTest1(string siteName, int batches)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new QueryPageGenerator(site, "Ancientpages") { PaginationSize = 100 };
            var pages = await generator.EnumPagesAsync().Take(100 * batches).ToListAsync();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task GetQueryPageNamesTest(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var sp = await QueryPageGenerator.GetQueryPageNamesAsync(site);
            Assert.Contains("Uncategorizedpages", sp);
            ShallowTrace(sp);
        }

        [Fact]
        public async Task WpTestGetSearchTest()
        {
            var site = await WpTest2SiteAsync;
            var generator = new SearchGenerator(site, "test") { PaginationSize = 20 };
            var pages = await generator.EnumPagesAsync().Take(200).ToListAsync();
            TracePages(pages);
            AssertTitlesDistinct(pages);
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
                new HashSet<string>(pages.Select(p => p.Title!)));
        }

    }
}
