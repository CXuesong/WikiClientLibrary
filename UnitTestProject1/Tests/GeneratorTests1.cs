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
            Utility.AssertTitlesDistinct(pages);
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
            Utility.AssertTitlesDistinct(pages);
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
            Utility.AssertTitlesDistinct(pages);
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
            Utility.AssertTitlesDistinct(pages);
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
            Utility.AssertTitlesDistinct(pages);
            var catInfo = cat.GetPropertyGroup<CategoryInfoPropertyGroup>();
            Utility.AssertNotNull(catInfo);
            Assert.Equal(catInfo.MembersCount, pages.Count);
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
            Utility.AssertTitlesDistinct(pages);
        }

        [Theory]
        [InlineData(nameof(WpTest2SiteAsync))]
        [InlineData(nameof(WikiaTestSiteAsync))]
        [InlineData(nameof(TFWikiSiteAsync))]
        public async Task QueryPageGeneratorTest2(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new QueryPageGenerator(site, "Ancientpages") { PaginationSize = 50 };
            var info = await generator.GetQueryPageResultInfoAsync();
            ShallowTrace(info);
            Assert.Equal("Ancientpages", info.Name);
            if (site.SiteInfo.Version.Above(1, 20))
            {
                Assert.True(info.IsCached);
                Assert.True(info.CachedTimestamp >= new DateTime(2022, 1, 1));
                Assert.True(info.CachedTimestamp < DateTime.UtcNow + TimeSpan.FromHours(1));
            }
            var items = await generator.EnumItemsAsync().Take(50).ToListAsync();
            ShallowTrace(items, 1);
            Assert.All(items, i => Assert.True(i.Timestamp > new DateTime(2005, 1, 1) && i.Timestamp < DateTime.UtcNow));
            var pages = await generator.EnumPagesAsync().Take(50).ToListAsync();
            TracePages(pages);
            Assert.Equal(items.Select(i => i.Title).OrderBy(t => t), pages.Select(p => p.Title).OrderBy(t => t));
        }

        [Theory]
        [InlineData(nameof(WpEnSiteAsync))]
        [InlineData(nameof(WpTest2SiteAsync))]
        public async Task QueryPageGeneratorTest3(string siteName)
        {
            var site = await WikiSiteFromNameAsync(siteName);
            var generator = new QueryPageGenerator(site, "GadgetUsage") { PaginationSize = 50 };
            var items = await generator.EnumItemsAsync().Take(50).ToListAsync();
            ShallowTrace(items, 1);
            Assert.NotEmpty(items);
            Assert.All(items, i => Assert.Contains("Gadget", i.Title, StringComparison.OrdinalIgnoreCase));
            var pages = await generator.EnumPagesAsync().Take(50).ToListAsync();
            TracePages(pages);
            Assert.All(pages, p => Assert.False(p.Exists));
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

    }
}
