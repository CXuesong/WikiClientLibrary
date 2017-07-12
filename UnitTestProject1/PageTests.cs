// Enables the following conditional switch in the project options
// to prevent test cases from making any edits.
//          DRY_RUN

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{

    public class PageTests : WikiSiteTestsBase
    {
        private const string SummaryPrefix = "WikiClientLibrary test. ";


        /// <inheritdoc />
        public PageTests(ITestOutputHelper output) : base(output)
        {
            SiteNeedsLogin(Utility.EntryPointWikipediaTest2);
            SiteNeedsLogin(Utility.EntryPointWikiaTest);
            SiteNeedsLogin(Utility.EntryWikipediaLzh);
        }

        [Fact]
        public async Task WpTest2PageReadTest1()
        {
            var site = await WpTest2SiteAsync;
            var page = new Page(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            ShallowTrace(page);
            Assert.True(page.Exists);
            Assert.Equal("Wikipedia:Sandbox", page.Title);
            Assert.Equal(4, page.NamespaceId);
            Assert.Equal("en", page.PageLanguage);
            // Chars vs. Bytes
            Assert.True(page.Content.Length <= page.ContentLength);
            Output.WriteLine(new string('-', 10));
            page = new Page(site, "file:inexistent_file.jpg");
            await page.RefreshAsync();
            ShallowTrace(page);
            Assert.False(page.Exists);
            Assert.Equal("File:Inexistent file.jpg", page.Title);
            Assert.Equal(6, page.NamespaceId);
            Assert.Equal("en", page.PageLanguage);
        }

        [Fact]
        public async Task WpTest2PageReadTest2()
        {
            var site = await WpTest2SiteAsync;
            var search = await site.OpenSearchAsync("A", 10);
            var pages = search.Select(e => new Page(site, e.Title)).ToList();
            await pages.RefreshAsync();
            ShallowTrace(pages);
        }

        [Fact]
        public async Task WpTest2PageReadRedirectTest()
        {
            var site = await WpTest2SiteAsync;
            var page = new Page(site, "Foo");
            await page.RefreshAsync();
            Assert.True(page.IsRedirect);
            var target = await page.GetRedirectTargetAsync();
            ShallowTrace(target);
            Assert.Equal("Foo24", target.Title);
            Assert.True(target.RedirectPath.SequenceEqual(new[] {"Foo", "Foo2", "Foo23"}));
        }

        [Fact]
        public async Task WpLzhPageReadDisambigTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new Page(site, "中國_(釋義)");
            await page.RefreshAsync();
            Assert.True(await page.IsDisambiguationAsync());
        }

        [Fact]
        public async Task WpLzhFetchRevisionsTest()
        {
            var site = await WpLzhSiteAsync;
            var revIds = new[] {248199, 248197, 255289};
            var pageTitles = new[] {"清", "清", "香草"};
            var rev = await Revision.FetchRevisionsAsync(site, revIds).ToList();
            ShallowTrace(rev);
            Assert.True(rev.Select(r => r.Id).SequenceEqual(revIds));
            Assert.True(rev.Select(r => r.Page.Title).SequenceEqual(pageTitles));
            // Asserts that pages with the same title shares the same reference
            // Or an Exception will raise.
            var pageDict = rev.Select(r => r.Page).Distinct().ToDictionary(p => p.Title);
        }

        [Fact]
        public async Task WpLzhFetchFileTest()
        {
            var site = await WpLzhSiteAsync;
            var file = new FilePage(site, "File:Empress Suiko.jpg");
            await file.RefreshAsync();
            ShallowTrace(file);
            //Assert.True(file.Exists);   //It's on WikiMedia!
            Assert.Equal(58865, file.LastFileRevision.Size);
            Assert.Equal("7aa12c613c156dd125212d85a072b250625ae39f", file.LastFileRevision.Sha1.ToLowerInvariant());
        }

        [Fact]
        public async Task WikiaPageReadTest()
        {
            var site = await WikiaTestSiteAsync;
            var page = new Page(site, "Project:Sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.Equal("Mediawiki 1.19 test Wiki:Sandbox", page.Title);
            Assert.Equal(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [Fact]
        public async Task WikiaPageReadDisambigTest()
        {
            var site = await WikiaTestSiteAsync;
            var page = new Page(site, "Test (Disambiguation)");
            await page.RefreshAsync();
            Assert.True(await page.IsDisambiguationAsync());
        }

        [Fact]
        public async Task WpTestEnumPageLinksTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new Page(site, site.SiteInfo.MainPage);
            Output.WriteLine(page.ToString());
            var links = await page.EnumLinksAsync().ToList();
            ShallowTrace(links);
            Assert.True(links.Contains("文言維基大典"));
            Assert.True(links.Contains("幫助:凡例"));
            Assert.True(links.Contains("維基大典:卓著"));
        }

        [Fact]
        public async Task WpLzhRedirectedPageReadTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new Page(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.ResolveRedirects);
            Assert.Equal("維基大典:沙盒", page.Title);
            Assert.Equal(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [Fact]
        public async Task WpTest2PageWriteTest1()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            var page = new Page(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            page.Content += "\n\nTest from WikiClientLibrary.";
            Output.WriteLine(page.Content);
            await page.UpdateContentAsync(SummaryPrefix + "Edit sandbox page.");
        }

        [Fact]
        public async Task WpTest2PageWriteTest2()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            var page = new Page(site, "Test page");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.True(page.Protections.Any(), "To perform this test, the working page should be protected.");
            page.Content += "\n\nTest from WikiClientLibrary.";
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a protected page."));
        }

        [Fact]
        public async Task WpTest2PageWriteTest3()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            var page = new Page(site, "Special:RecentChanges");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.True(page.IsSpecialPage);
            page.Content += "\n\nTest from WikiClientLibrary.";
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a special page."));
        }

        [Fact]
        public async Task WpTest2BulkPurgeTest()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            // Usually 500 is the limit for normal users.
            var pages = new AllPagesGenerator(site) {PagingSize = 300}.EnumPages().Take(300).ToList();
            var badPage = new Page(site, "Inexistent page title");
            pages.Insert(pages.Count/2, badPage);
            Output.WriteLine("Attempt to purge: ");
            ShallowTrace(pages, 1);
            // Do a normal purge. It may take a while.
            var failedPages = await pages.PurgeAsync();
            Output.WriteLine("Failed pages: ");
            ShallowTrace(failedPages, 1);
            Assert.Equal(1, failedPages.Count);
            Assert.Same(badPage, failedPages.Single());
        }

        [Fact]
        public async Task WpTest2PagePurgeTest()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            // We do not need to login.
            var page = new Page(site, "project:sandbox");
            var result = await page.PurgeAsync(PagePurgeOptions.ForceLinkUpdate | PagePurgeOptions.ForceRecursiveLinkUpdate);
            Assert.True(result);
            // Now an ArgumentException should be thrown from Page.ctor.
            //page = new Page(site, "special:");
            //result = AwaitSync(page.PurgeAsync());
            //Assert.False(result);
            page = new Page(site, "the page should be inexistent");
            result = await page.PurgeAsync();
            Assert.False(result);
        }

        [Fact]
        public async Task WikiaPageWriteTest1()
        {
            AssertModify();
            var site = await WikiaTestSiteAsync;
            Utility.AssertLoggedIn(site);
            var page = new Page(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            page.Content += "\n\nTest from WikiClientLibrary.";
            Output.WriteLine(page.Content);
            await page.UpdateContentAsync(SummaryPrefix + "Edit sandbox page.");
        }
    }
}
