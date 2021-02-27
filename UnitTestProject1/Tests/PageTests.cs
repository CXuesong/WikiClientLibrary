// Enables the following conditional switch in the project options
// to prevent test cases from making any edits.
//          DRY_RUN

using System;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Files;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class PageTests : WikiSiteTestsBase
    {
        private const string SummaryPrefix = "WikiClientLibrary test. ";


        /// <inheritdoc />
        public PageTests(ITestOutputHelper output) : base(output)
        {
            SiteNeedsLogin(Endpoints.WikipediaTest2);
            SiteNeedsLogin(Endpoints.WikiaTest);
            SiteNeedsLogin(Endpoints.WikipediaLzh);
        }

        [Fact]
        public async Task WpTest2PageReadTest1()
        {
            var site = await WpTest2SiteAsync;
            var page = new WikiPage(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            ShallowTrace(page);
            Assert.True(page.Exists);
            Assert.Equal("Wikipedia:Sandbox", page.Title);
            Assert.Equal(4, page.NamespaceId);
            Assert.Equal("en", page.PageLanguage);
            Utility.AssertNotNull(page.Content);
            // Chars vs. Bytes
            Assert.True(page.Content.Length <= page.ContentLength);
            Output.WriteLine(new string('-', 10));

            var page2 = new WikiPage(site, 2076);
            await page2.RefreshAsync();
            Assert.Equal(page.PageStub, page2.PageStub);

            var page3 = new WikiPage(site, "file:inexistent_file.jpg");
            await page3.RefreshAsync();
            ShallowTrace(page3);
            Assert.False(page3.Exists);
            Assert.Equal("File:Inexistent file.jpg", page3.Title);
            Assert.Equal(6, page3.NamespaceId);
            Assert.Equal("en", page3.PageLanguage);
        }

        [Fact]
        public async Task WpTest2PageReadTest2()
        {
            var site = await WpTest2SiteAsync;
            var search = await site.OpenSearchAsync("A", 10);
            var pages = search.Select(e => new WikiPage(site, e.Title)).ToList();
            await pages.RefreshAsync();
            ShallowTrace(pages);
        }

        [Fact]
        public async Task WpTest2PageReadRedirectTest()
        {
            var site = await WpTest2SiteAsync;
            var page = new WikiPage(site, "Foo");
            await page.RefreshAsync();
            Assert.True(page.IsRedirect);
            var target = await page.GetRedirectTargetAsync();
            ShallowTrace(target);
            Utility.AssertNotNull(target);
            Assert.Equal("Foo24", target.Title);
            Assert.True(target.RedirectPath.SequenceEqual(new[] { "Foo", "Foo2", "Foo23" }));
        }

        [Fact]
        public async Task WpEnPageGeoCoordinateTest()
        {
            var site = await WpEnSiteAsync;
            var page = new WikiPage(site, "Paris");
            await page.RefreshAsync(new WikiPageQueryProvider
            {
                Properties =
                {
                    new GeoCoordinatesPropertyProvider()
                }
            });
            ShallowTrace(page);
            var coordinate = page.GetPropertyGroup<GeoCoordinatesPropertyGroup>();
            Utility.AssertNotNull(coordinate);
            ShallowTrace(coordinate);
            Assert.False(coordinate.PrimaryCoordinate.IsEmpty);
            Assert.Equal(48.856613, coordinate.PrimaryCoordinate.Latitude, 5);
            Assert.Equal(2.352222, coordinate.PrimaryCoordinate.Longitude, 5);
            Assert.Equal(GeoCoordinate.Earth, coordinate.PrimaryCoordinate.Globe);
        }

        [Fact]
        public async Task WpLzhPageExtractTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new WikiPage(site, "莎拉伯恩哈特");
            await page.RefreshAsync(new WikiPageQueryProvider
            {
                Properties =
                {
                    new ExtractsPropertyProvider
                    {
                        AsPlainText = true,
                        IntroductionOnly = true,
                        MaxSentences = 1
                    }
                }
            });
            ShallowTrace(page);
            Assert.Equal("莎拉·伯恩哈特，一八四四年生，法國巴黎人也。", page.GetPropertyGroup<ExtractsPropertyGroup>()!.Extract);
        }

        [Fact]
        public async Task WpLzhPageImagesTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new WikiPage(site, "挪威");
            await page.RefreshAsync(new WikiPageQueryProvider
            {
                Properties =
                {
                    new PageImagesPropertyProvider
                    {
                        QueryOriginalImage = true,
                        ThumbnailSize = 100
                    }
                }
            });
            var group = page.GetPropertyGroup<PageImagesPropertyGroup>();
            ShallowTrace(group);
            Utility.AssertNotNull(group);
            Assert.Equal("Flag_of_Norway.svg", group.ImageTitle);
            Assert.Equal("https://upload.wikimedia.org/wikipedia/commons/d/d9/Flag_of_Norway.svg", group.OriginalImage.Url);
            Assert.Equal("https://upload.wikimedia.org/wikipedia/commons/thumb/d/d9/Flag_of_Norway.svg/100px-Flag_of_Norway.svg.png", group.ThumbnailImage.Url);
            Assert.Equal(100, Math.Max(group.ThumbnailImage.Width, group.ThumbnailImage.Height));
        }

        [Fact]
        public async Task WpLzhPageLanguageLinksTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new WikiPage(site, "莎拉伯恩哈特");
            await page.RefreshAsync(new WikiPageQueryProvider { Properties = { new LanguageLinksPropertyProvider(LanguageLinkProperties.Autonym) } });
            var langLinks = page.GetPropertyGroup<LanguageLinksPropertyGroup>()?.LanguageLinks;
            ShallowTrace(langLinks);
            Utility.AssertNotNull(langLinks);
            Assert.True(langLinks.Count > 120);
            var langLink = langLinks.FirstOrDefault(l => l.Language == "en");
            Utility.AssertNotNull(langLink);
            Assert.Equal("Sarah Bernhardt", langLink.Title);
            Assert.Equal("English", langLink.Autonym);
            // We didn't ask for URL so this should be null.
            Assert.All(langLinks, l => Assert.Null(l.Url));
            // Try out whether we still can fetch complete prop values even in the case of prop pagination.
            var pages = new[] { "挪威", "坤輿", "維基共享" }.Select(t => new WikiPage(site, t)).Append(page).ToList();
            await pages.RefreshAsync(new WikiPageQueryProvider { Properties = { new LanguageLinksPropertyProvider() } });
            Output.WriteLine("Language links ----");
            foreach (var p in pages)
                Output.WriteLine("{0}: {1}", p, p.GetPropertyGroup<LanguageLinksPropertyGroup>()?.LanguageLinks.Count);
            Assert.All(pages, p => Assert.True(p.GetPropertyGroup<LanguageLinksPropertyGroup>()!.LanguageLinks.Count > 50));
            Assert.Equal(langLinks.ToDictionary(l => l.Language, l => l.Title),
                page.GetPropertyGroup<LanguageLinksPropertyGroup>()!.LanguageLinks.ToDictionary(l => l.Language, l => l.Title));
        }

        [Fact]
        public async Task WpLzhPageReadDisambigTest()
        {
            var site = await WpLzhSiteAsync;
            var page1 = new WikiPage(site, "莎拉伯恩哈特");
            var page2 = new WikiPage(site, "中國_(釋義)");
            await new[] { page1, page2 }.RefreshAsync();
            Assert.False(await page1.IsDisambiguationAsync());
            Assert.True(await page2.IsDisambiguationAsync());
        }

        [Fact]
        public async Task WpLzhFetchRevisionsTest()
        {
            var site = await WpLzhSiteAsync;
            var revIds = new[] { 248199, 248197, 255289 };
            var pageTitles = new[] { "清", "清", "香草" };
            var rev = await Revision.FetchRevisionsAsync(site, revIds).ToListAsync();
            ShallowTrace(rev);
            Assert.Equal(revIds, rev.Select(r => r!.Id));
            Assert.Equal(pageTitles, rev.Select(r => r!.Page.Title));
            // Asserts that pages with the same title shares the same reference
            // Or an Exception will raise.
            var pageDict = rev.Select(r => r!.Page).Distinct().ToDictionary(p => p.Title!);
        }

        [Fact]
        public async Task WpLzhFetchFileTest()
        {
            var site = await WpLzhSiteAsync;
            var file = new WikiPage(site, "File:Empress Suiko.jpg");
            await file.RefreshAsync();
            ShallowTrace(file);
            Assert.False(file.Exists);      //It's on Wikimedia!
            Utility.AssertNotNull(file.LastFileRevision);
            Assert.Equal(58865, file.LastFileRevision.Size);
            Assert.Equal("7aa12c613c156dd125212d85a072b250625ae39f", file.LastFileRevision.Sha1.ToLowerInvariant());
            Assert.Empty(file.LastFileRevision.ExtMetadata);
        }

        [Fact]
        public async Task WpLzhFetchFileWithExtMetadataTest()
        {
            var site = await WpLzhSiteAsync;
            var file = new WikiPage(site, "File:Empress Suiko.jpg");
            await file.RefreshAsync(new WikiPageQueryProvider
            {
                Properties =
                {
                    new FileInfoPropertyProvider { QueryExtMetadata = true }
                }
            });

            Output.WriteLine("Fetched file:");
            ShallowTrace(file);
            Utility.AssertNotNull(file.LastFileRevision);
            Output.WriteLine("ExtMetadata:");
            ShallowTrace(file.LastFileRevision.ExtMetadata);

            Utility.AssertNotNull(file.LastFileRevision.ExtMetadata);
            Assert.True(new DateTime(2013, 11, 14, 12, 15, 30) <= (DateTime)file.LastFileRevision.ExtMetadata["DateTime"].Value);
            Assert.Equal(FileRevisionExtMetadataValueSources.MediaWikiMetadata, file.LastFileRevision.ExtMetadata["DateTime"]?.Source);
        }

        [Fact]
        public async Task WikiaPageReadTest()
        {
            var site = await WikiaTestSiteAsync;
            var page = new WikiPage(site, "Project:Sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.Equal("Dman Wikia:Sandbox", page.Title);
            Assert.Equal(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [Fact]
        public async Task WikiaPageReadByIdTest()
        {
            var site = await WikiaTestSiteAsync;
            var page = new WikiPage(site, 637);
            await page.RefreshAsync();
            Assert.Equal("Dman Wikia:Sandbox", page.Title);
            Assert.Equal(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [Fact]
        public async Task WikiaPageReadDisambigTest()
        {
            var site = await WikiaTestSiteAsync;
            Output.WriteLine("Is there Disambiguator on this site? {0}", site.Extensions.Contains("Disambiguator"));
            var page = new WikiPage(site, "Test (Disambiguation)");
            await page.RefreshAsync();
            Assert.True(await page.IsDisambiguationAsync());
        }

        [Fact]
        public async Task WpLzhRedirectedPageReadTest()
        {
            var site = await WpLzhSiteAsync;
            var page = new WikiPage(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.ResolveRedirects);
            Assert.Equal("維基大典:沙盒", page.Title);
            Assert.Equal(4, page.NamespaceId);
            ShallowTrace(page);
        }

        [SkippableFact]
        public async Task WpTest2PageWriteTest1()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            var page = new WikiPage(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            page.Content += "\n\nTest from WikiClientLibrary.";
            Output.WriteLine(page.Content);
            await page.UpdateContentAsync(SummaryPrefix + "Edit sandbox page.");
        }

        [SkippableFact]
        public async Task WpTest2PageWriteTest2()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            var page = new WikiPage(site, "Test page");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.True(page.Protections.Any(), "To perform this test, the working page should be protected.");
            page.Content += "\n\nTest from WikiClientLibrary.";
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a protected page."));
        }

        [SkippableFact]
        public async Task WpTest2PageWriteTest3()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            var page = new WikiPage(site, "Special:RecentChanges");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.True(page.IsSpecialPage);
            page.Content += "\n\nTest from WikiClientLibrary.";
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                page.UpdateContentAsync(SummaryPrefix + "Attempt to edit a special page."));
        }

        [SkippableFact]
        public async Task WpTest2BulkPurgeTest()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            // Usually 500 is the limit for normal users.
            var pages = await new AllPagesGenerator(site) { PaginationSize = 300 }.EnumPagesAsync().Take(300).ToListAsync();
            var badPage = new WikiPage(site, "Inexistent page title");
            pages.Insert(pages.Count / 2, badPage);
            Output.WriteLine("Attempt to purge: ");
            ShallowTrace(pages, 1);
            // Do a normal purge. It may take a while.
            var failedPages = await pages.PurgeAsync();
            Output.WriteLine("Failed pages: ");
            ShallowTrace(failedPages, 1);
            Assert.Equal(1, failedPages.Count);
            Assert.Equal(badPage.Title, failedPages.Single().Page.Title);
        }

        [SkippableFact]
        public async Task WpTest2PagePurgeTest()
        {
            AssertModify();
            var site = await WpTest2SiteAsync;
            // We do not need to login.
            var page = new WikiPage(site, "project:sandbox");
            var result = await page.PurgeAsync(PagePurgeOptions.ForceLinkUpdate | PagePurgeOptions.ForceRecursiveLinkUpdate);
            Assert.True(result);
            // Now an ArgumentException should be thrown from Page.ctor.
            //page = new Page(site, "special:");
            //result = AwaitSync(page.PurgeAsync());
            //Assert.False(result);
            page = new WikiPage(site, "the page should be inexistent");
            result = await page.PurgeAsync();
            Assert.False(result);
        }

        [SkippableFact]
        public async Task WikiaPageWriteTest1()
        {
            AssertModify();
            var site = await WikiaTestSiteAsync;
            Utility.AssertLoggedIn(site);
            var page = new WikiPage(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            page.Content += "\n\nTest from WikiClientLibrary.";
            Output.WriteLine(page.Content);
            await page.UpdateContentAsync(SummaryPrefix + "Edit sandbox page.");
        }
    }
}
