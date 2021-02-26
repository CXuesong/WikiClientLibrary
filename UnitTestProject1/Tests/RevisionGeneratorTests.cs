using System;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class RevisionGeneratorTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public RevisionGeneratorTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task WpTestEnumRevisionsTest1()
        {
            var site = await WpTest2SiteAsync;
            var page = new WikiPage(site, "Page:Edit_page_for_chrome");
            var gen = page.CreateRevisionsGenerator();
            gen.PaginationSize = 20;
            var revisions = await gen.EnumItemsAsync().Skip(5).Take(5).ToListAsync();
            Assert.Equal(5, revisions.Count);
            Assert.True(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }

        [Fact]
        public async Task WpTestEnumRevisionsTest2()
        {
            var site = await WpTest2SiteAsync;
            // 5,100 revisions in total
            var page = new WikiPage(site, "Page:Edit_page_for_chrome");
            var gen = page.CreateRevisionsGenerator();
            gen.PaginationSize = 500;
            var revisions = await gen.EnumItemsAsync().Take(2000).ToListAsync();
            Assert.True(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }


        [Fact]
        public async Task WpTestEnumRevisionsTest3()
        {
            var site = await WpTest2SiteAsync;
            // 5,100 revisions in total
            var page = new WikiPage(site, "Page:Edit_page_for_chrome");
            var t1 = new DateTime(2014, 10, 20, 10, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2014, 10, 22, 10, 0, 0, DateTimeKind.Utc);
            var gen = new RevisionsGenerator(page.Site, page.Title)
            {
                TimeAscending = true,
                StartTime = t1,
                EndTime = t2,
                PaginationSize = 30,
            };
            var revisions = await gen.EnumItemsAsync().Take(50).ToListAsync();
            Assert.True(revisions.SequenceEqual(revisions.OrderBy(r => r.TimeStamp)));
            Assert.True(revisions.First().TimeStamp >= t1);
            Assert.True(revisions.Last().TimeStamp <= t2);
            // This holds on 2016-12-09
            Assert.Equal(32, revisions.Count);
            ShallowTrace(revisions);
        }

        [Fact]
        public async Task WikiaEnumRevisionsTest1()
        {
            var site = await WikiaTestSiteAsync;
            var page = new WikiPage(site, "Project:Sandbox");
            var gen = page.CreateRevisionsGenerator();
            gen.PaginationSize = 20;
            var revisions = await gen.EnumItemsAsync().Skip(5).Take(5).ToListAsync();
            Assert.All(revisions, Utility.AssertNotNull);
            Assert.Equal(5, revisions.Count);
            Assert.True(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }

        [Fact]
        public async Task WikiaEnumRevisionsTest2()
        {
            var site = await WikiaTestSiteAsync;
            var page = new WikiPage(site, "Project:Sandbox");
            var gen = page.CreateRevisionsGenerator();
            gen.PaginationSize = 500;
            var revisions = await gen.EnumItemsAsync().Take(2000).ToListAsync();
            Assert.True(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }

    }
}
