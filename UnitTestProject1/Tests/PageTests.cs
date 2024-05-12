// Enables  to prevent test cases from making any edits.
//          DRY_RUN

using System.Text;
using WikiClientLibrary.Files;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class PageTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
{

    private const string SummaryPrefix = "WikiClientLibrary test.";

    /// <inheritdoc />
    public PageTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
    {
            SiteNeedsLogin(Endpoints.WikipediaTest2);
            SiteNeedsLogin(Endpoints.WikiaTest);
            SiteNeedsLogin(Endpoints.WikipediaLzh);
            SiteNeedsLogin(Endpoints.TFWiki);
        }

    [Fact]
    public async Task WpEnPageReadTest1()
    {
            var site = await WpEnSiteAsync;
            var page = new WikiPage(site, "test");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            ShallowTrace(page);
            Assert.True(page.Exists);
            Assert.Equal("Test", page.Title);
            Assert.Equal(BuiltInNamespaces.Main, page.NamespaceId);
            Assert.Equal("en", page.PageLanguage);
            Utility.AssertNotNull(page.Content);
            // Bytes v.s. Chars
            Assert.Equal(page.ContentLength, Encoding.UTF8.GetByteCount(page.Content));
            Assert.True(page.Content.Length > 100, "The page content looks abnormally too short.");
            Output.WriteLine(new string('-', 10));

            var page2 = new WikiPage(site, 11089416);
            await page2.RefreshAsync();
            Assert.Equal(page.PageStub, page2.PageStub);

            var page3 = new WikiPage(site, "file:inexistent_file.jpg");
            await page3.RefreshAsync();
            ShallowTrace(page3);
            Assert.False(page3.Exists);
            Assert.Equal("File:Inexistent file.jpg", page3.Title);
            Assert.Equal(BuiltInNamespaces.File, page3.NamespaceId);
            Assert.Equal("en", page3.PageLanguage);
        }

    [Theory]
    [InlineData(nameof(WpTest2SiteAsync), "Project:sandbox", "Wikipedia:Sandbox", BuiltInNamespaces.Project, 2076)]
    [InlineData(nameof(WikiaTestSiteAsync), "Project:sandbox", "Dman Wikia:Sandbox", BuiltInNamespaces.Project, 637)]
    [InlineData(nameof(TFWikiSiteAsync), "Help:coming soon", "Help:Coming soon", BuiltInNamespaces.Help, 10122)]
    public async Task WikiPageReadTest2(string siteName, string fetchTitle, string expectedTitle, int expectedNs, int expectedId)
    {
            var site = await WikiSiteFromNameAsync(siteName);
            var page = new WikiPage(site, fetchTitle);
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Output.WriteLine("Fetch by title: {0}", fetchTitle);
            ShallowTrace(page);
            Assert.True(page.Exists);
            Assert.Equal(expectedTitle, page.Title);
            Assert.Equal(expectedNs, page.NamespaceId);
            Assert.Equal(expectedId, page.Id);
            Utility.AssertNotNull(page.Content);
            Assert.True(page.ContentLength >= page.Content.Length, "ContentLength (in bytes) should be equal or greater than content characters.");

            Output.WriteLine("Fetch by ID: {0}", expectedId);
            page = new WikiPage(site, expectedId);
            Assert.Null(page.Content);
            await page.RefreshAsync();
            ShallowTrace(page);

            Assert.True(page.Exists);
            Assert.Equal(expectedTitle, page.Title);
            Assert.Equal(expectedNs, page.NamespaceId);
            Assert.Equal(expectedId, page.Id);
            // Since we are not fetching content, content should be null.
            Assert.Null(page.Content);
            // but reivision info should exist.
            Assert.NotNull(page.LastRevision);
        }

    [Theory]
    [InlineData(nameof(WikiaTestSiteAsync), "Test (Disambiguation)", true)]
    [InlineData(nameof(WpLzhSiteAsync), "莎拉伯恩哈特", false)]
    [InlineData(nameof(WpLzhSiteAsync), "中國_(釋義)", true)]
    [InlineData(nameof(TFWikiSiteAsync), "Cybertron (disambiguation)", true)]
    public async Task WikiPageReadDisambigTest(string siteName, string pageTitle, bool isDab)
    {
            var site = await WikiSiteFromNameAsync(siteName);
            Output.WriteLine("Is there Disambiguator on this site? {0}", site.Extensions.Contains("Disambiguator"));
            var page = new WikiPage(site, pageTitle);
            await page.RefreshAsync();
            Assert.True(page.Exists);
            Assert.Equal(isDab, await page.IsDisambiguationAsync());
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
            Assert.Equal(new[] { "Foo", "Foo2", "Foo23" }, target.RedirectPath);
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
            Assert.Equal(48.85667, coordinate.PrimaryCoordinate.Latitude, 5);
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
    public async Task WpLzhFetchRevisionsTest()
    {
            var site = await WpLzhSiteAsync;
            var revIds = new[] { 248199L, 248197, 255289 };
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
    public async Task WpLzhRedirectedPageReadTest()
    {
            var site = await WpLzhSiteAsync;
            var page = new WikiPage(site, "project:sandbox");
            await page.RefreshAsync(PageQueryOptions.ResolveRedirects);
            Assert.Equal("維基大典:沙盒", page.Title);
            Assert.Equal(4, page.NamespaceId);
            ShallowTrace(page);
        }

    [SkippableTheory]
    [InlineData(nameof(WpTest2SiteAsync), "project:sandbox")]
    [InlineData(nameof(WikiaTestSiteAsync), "project:sandbox")]
    [InlineData(nameof(TFWikiSiteAsync), "User:FuncGammaBot/Sandbox")]
    public async Task WikiPageWriteTest1(string siteName, string pageTitle)
    {
            AssertModify();
            var site = await WikiSiteFromNameAsync(siteName);
            var page = new WikiPage(site, pageTitle);
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            // As a precaution, we don't create new page by editing.
            Assert.True(page.Exists);
            Output.WriteLine(page.Content);
            await page.EditAsync(new WikiPageEditOptions
            {
                Content = page.Content + "\n\nTest from WikiClientLibrary.",
                Summary = SummaryPrefix + "Edit sandbox page.",
                Minor = true,
                Bot = true,
            });
        }

    [SkippableTheory]
    [InlineData(nameof(WpTest2SiteAsync), "Test page")]
    public async Task WikiPageWriteTest2(string siteName, string pageTitle)
    {
            AssertModify();
            var site = await WikiSiteFromNameAsync(siteName);
            var page = new WikiPage(site, pageTitle);
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.True(page.Protections.Any(), "To perform this test, the working page should be protected.");
            await Assert.ThrowsAsync<UnauthorizedOperationException>(async () =>
                await page.EditAsync(new WikiPageEditOptions
                {
                    Content = page.Content + "\n\nTest from WikiClientLibrary.",
                    Summary = SummaryPrefix + "Attempt to edit a protected page.",
                    Minor = false,
                    Bot = true,
                }));
        }

    [SkippableTheory]
    [InlineData(nameof(WpTest2SiteAsync))]
    [InlineData(nameof(TFWikiSiteAsync))]
    public async Task WikiPageWriteTest3(string siteName)
    {
            AssertModify();
            var site = await WikiSiteFromNameAsync(siteName);
            var page = new WikiPage(site, "Special:RecentChanges");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            Assert.True(page.IsSpecialPage);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await page.EditAsync(new WikiPageEditOptions
                {
                    Content = page.Content + "\n\nTest from WikiClientLibrary.",
                    Summary = SummaryPrefix + "Attempt to edit a special page.",
                }));
        }

    [SkippableTheory]
    [InlineData(nameof(WpTest2SiteAsync), "project:sandbox")]
    [InlineData(nameof(WikiaTestSiteAsync), "project:sandbox")]
    [InlineData(nameof(TFWikiSiteAsync), "User:FuncGammaBot/Sandbox")]
    public async Task WikiPageWriteSectionTest1(string siteName, string pageTitle)
    {
            AssertModify();
            var site = await WikiSiteFromNameAsync(siteName);
            var page = new WikiPage(site, pageTitle);
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            // As a precaution, we don't create new page by editing.
            Assert.True(page.Exists);
            WriteOutput("Existing content");
            WriteOutput(page.Content);
            WriteOutput("Existing sections");
            var sections = Utility.WikitextParseSections(page.Content!);
            ShallowTrace(sections);

            WriteOutput("Adding a section");
            var newSectionTitle = "New section " + DateTime.UtcNow.ToString("O");
            const string PLACEHOLDER = "<Placeholder>";
            var newSectionContent = "Test from WikiClientLibrary.\n\nSection content: " + PLACEHOLDER;
            await page.AddSectionAsync(newSectionTitle, new WikiPageEditOptions
            {
                Content = newSectionContent,
                Summary = SummaryPrefix + $"Append section ([[#{newSectionTitle}]]).",
                Minor = true,
                Bot = true,
            });

            WriteOutput("Updated content");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            WriteOutput(page.Content);
            Assert.Contains($"== {newSectionTitle} ==\n\n{newSectionContent}", page.Content);
            WriteOutput("Existing sections");
            sections = Utility.WikitextParseSections(page.Content!);
            ShallowTrace(sections);

            // figure out the section number for editing
            var sectionIndex = sections.FindIndex(s => s.Title == newSectionTitle);
            WriteOutput("New section index: {0}", sectionIndex);
            Assert.True(sectionIndex >= 0);

            WriteOutput("Editing a section");
            newSectionContent = sections[sectionIndex].Content.Replace(PLACEHOLDER, Guid.NewGuid().ToString());

            // section #0: content before first heading
            await page.EditSectionAsync(sectionIndex.ToString(), new WikiPageEditOptions
            {
                Content = newSectionContent,
                Summary = SummaryPrefix + $"Edit section ([[#{newSectionTitle}]]).",
                Minor = true,
                Bot = true,
            });

            WriteOutput("Updated content");
            await page.RefreshAsync(PageQueryOptions.FetchContent);
            WriteOutput(page.Content);
            Assert.Contains(newSectionContent, page.Content);
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

}