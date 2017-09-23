using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
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
                if (page is CategoryPage cat)
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
            var generator = new AllPagesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpAllPagesGeneratorTest2()
        {
            var site = await WpTest2SiteAsync;
            var generator = new AllPagesGenerator(site) {StartTitle = "W", PagingSize = 20};
            var pages = generator.EnumPages(PageQueryOptions.FetchContent).Take(100).ToList();
            TracePages(pages);
            Assert.True(pages[0].Title[0] == 'W');
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WikiaAllPagesGeneratorTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new AllPagesGenerator(site) {NamespaceId = BuiltInNamespaces.Template};
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpAllCategoriesGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var generator = new AllCategoriesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            generator = new AllCategoriesGenerator(site) {StartTitle = "C", PagingSize = 20};
            pages = generator.EnumPages(PageQueryOptions.FetchContent).Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WikiaAllCategoriesGeneratorTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new AllCategoriesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpCategoryMembersGeneratorTest()
        {
            var site = await WpTest2SiteAsync;
            var cat = new CategoryPage(site, "Category:Template documentation pages‏‎");
            await cat.RefreshAsync();
            Output.WriteLine(cat.ToString());
            var generator = new CategoryMembersGenerator(cat) {PagingSize = 50};
            var pages = generator.EnumPages().ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            Assert.Equal(cat.MembersCount, pages.Count);
        }

        [Fact]
        public async Task WikiaCategoryMembersGeneratorTest()
        {
            var site = await WikiaTestSiteAsync;
            var cat = new CategoryPage(site, "Category:BlogListingPage‏‎‏‎");
            await cat.RefreshAsync();
            Output.WriteLine(cat.ToString());
            var generator = new CategoryMembersGenerator(cat) {PagingSize = 50};
            var pages = generator.EnumPages().ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            Assert.Equal(cat.MembersCount, pages.Count);
        }


        [Fact]
        public async Task WpTest2RecentChangesGeneratorTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new RecentChangesGenerator(site) {LastRevisionsOnly = true, PagingSize = 20};
            var pages = generator.EnumPages().Take(1000).ToList();
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
            var pages = generator.EnumPages(PageQueryOptions.FetchContent).Take(100).ToList();
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
                TypeFilters = RecentChangesFilterTypes.Edit
            };
            var pages = generator.EnumPages().Take(2000).ToList();
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
            };
            var rc = generator.EnumRecentChanges().Take(2000).ToList();
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
        public async Task WikiaRecentChangesListTest()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
            };
            var rc = generator.EnumRecentChanges().Take(2000).ToList();
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
            var rc = generator.EnumRecentChanges().Take(2).ToList();
            Skip.If(rc.Count < 1);
            // We haven't logged in.
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() => rc[0].PatrolAsync());
        }

        [Fact]
        public async Task WpQueryPageGeneratorTest1()
        {
            var site = await WpTest2SiteAsync;
            var generator = new QueryPageGenerator(site, "Ancientpages");
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WikiaQueryPageGeneratorTest1()
        {
            var site = await WikiaTestSiteAsync;
            var generator = new QueryPageGenerator(site, "Ancientpages");
            var pages = generator.EnumPages().Take(2000).ToList();
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
            var generator = new SearchGenerator(site, "test") {PagingSize = 20};
            var pages = generator.EnumPages().Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [Fact]
        public async Task WpLzhSearchTest()
        {
            var site = await WpLzhSiteAsync;
            var generator = new SearchGenerator(site, "維基");
            var pages = generator.EnumPages().Take(50).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            // Note as 2017-03-07, [[維基]] actually exists on lzh wiki, but it's a redirect to [[維基媒體基金會]].
            // Maybe that's why it's not included in the search result.
            //Assert.True(pages.Any(p => p.Title == "維基"));
            Assert.Contains(pages, p => p.Title == "維基媒體基金會");
            Assert.Contains(pages, p => p.Title == "維基大典");
            Assert.Contains(pages, p => p.Title == "文言維基大典");
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
            var blg = new BacklinksGenerator(site, "Albert Einstein‏‎") {PagingSize = 100};
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
            var tig = new TranscludedInGenerator(site, "Module:Portal‏‎") { PagingSize = 100 };
            var pages = await tig.EnumPagesAsync().Take(100).ToList();
            ShallowTrace(pages, 1);
            Assert.Contains(pages, p => p.Title == "Template:Portal bar");
        }

    }
}
