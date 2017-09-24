// To prevent test cases from making any edits, See WikiSiteTestsBase.cs.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static UnitTestProject1.Utility;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    /// <summary>
    /// The tests in this class requires a site administrator (i.e. sysop) account.
    /// </summary>
    public class PageTestsDirty :  WikiSiteTestsBase
    {
        private const string SummaryPrefix = "WikiClientLibrary test. ";

        // The following pages will be created.
        private const string TestPage1Title = "WCL test page 1";

        private const string TestPage11Title = "WCL test page 1/1";

        private const string TestPage2Title = "WCL test page 2";

        // The following pages will NOT be created at first.
        private const string TestPage12Title = "WCL test page 1/2";

        public Task<WikiSite> SiteAsync => GetWikiSiteAsync(CredentialManager.DirtyTestsEntryPointUrl);

        /// <inheritdoc />
        public PageTestsDirty(ITestOutputHelper output) : base(output)
        {
            if (CredentialManager.DirtyTestsEntryPointUrl == null)
                throw new SkipException("You need to specify CredentialManager.DirtyTestsEntryPointUrl before running this group of tests.");
            SiteNeedsLogin(CredentialManager.DirtyTestsEntryPointUrl);
        }

        private async Task<WikiPage> GetOrCreatePage(WikiSite site, string title)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var page = new WikiPage(site, title);
            await page.RefreshAsync();
            if (!page.Exists)
            {
                Output.WriteLine("Creating page: " + page);
                page.Content = $@"<big>This is a test page for '''WikiClientLibrary'''.</big>

This page is created by an automated program for unit test purposes.

If you see this page '''OUTSIDE''' a test wiki site,
maybe you should consider viewing the history of the page,
and find out who created the page accidentally.

The original title of the page is '''{title}'''.

== See also ==
* [[Special:PrefixIndex/{title}/|Subpages]]
";
                await page.UpdateContentAsync(SummaryPrefix + "Create test page for unit tests.");
            }
            return page;
        }

        [Fact]
        public async Task PageMoveAndDeleteTest1()
        {
            var page1 = new WikiPage(await SiteAsync, TestPage11Title);
            var page2 = new WikiPage(await SiteAsync, TestPage12Title);
            Output.WriteLine("Deleted:" + await page2.DeleteAsync(SummaryPrefix + "Delete the move destination."));
            await page1.MoveAsync(TestPage12Title, SummaryPrefix + "Move a page.", PageMovingOptions.IgnoreWarnings);
            await page2.DeleteAsync(SummaryPrefix + "Delete the moved page.");
        }

        [Theory]
        [InlineData("File:Test image.jpg", "1")]
        [InlineData("File:Test image.jpg", "2")]
        [InlineData("Test image.jpg" ,"1")]
        public async Task LocalFileUploadTest1(string fileName, string imageName)
        {
            const string ReuploadSuffix = "\n\nReuploaded.";
            var file = GetDemoImage(imageName);
            UploadResult result;
            try
            {
                result = await FilePage.UploadAsync(await SiteAsync, file.ContentStream, fileName, file.Description, false);
            }
            catch (UploadException ex)
            {
                // Client should rarely be checking like this
                // Usually we should notify the user.
                if (ex.UploadResult.Warnings[0].Key == "exists")
                    // Just re-upload
                    result = await FilePage.UploadAsync(await SiteAsync, ex.UploadResult, fileName,
                        file.Description + ReuploadSuffix, true);
                else
                    throw;
            }
            ShallowTrace(result);
            var fp = new FilePage(await SiteAsync, fileName);
            await fp.RefreshAsync();
            ShallowTrace(fp);
            Assert.True(fp.Exists);
            Assert.Equal(file.Sha1, fp.LastFileRevision.Sha1.ToUpperInvariant());
        }

        [Fact]
        public async Task LocalFileUploadRetryTest1()
        {
            const string FileName = "File:Test image.jpg";
            var localSite = await CreateIsolatedWikiSiteAsync(CredentialManager.DirtyTestsEntryPointUrl);
            // Cahce the token first so it won't be affected by the short timeout.
            await localSite.GetTokenAsync("edit");
            Output.WriteLine("Try uploading…");
            // We want to timeout and retry.
            localSite.WikiClient.Timeout = TimeSpan.FromSeconds(0.5);
            localSite.WikiClient.RetryDelay = TimeSpan.FromSeconds(1);
            localSite.WikiClient.MaxRetries = 1;
            var buffer = new byte[1024 * 1024 * 2];     // 2MB, I think this size is fairly large.
                                                        // If your connection speed is too fast then, well, trottle it plz.
            using (var ms = new MemoryStream(buffer))
            {
                await Assert.ThrowsAsync<TimeoutException>(async () =>
                {
                    try
                    {
                        await FilePage.UploadAsync(localSite, ms, FileName,
                            "This is an upload that is destined to fail. Upload timeout test.", false);
                    }
                    catch (UploadException ex)
                    {
                        throw new SkipException(
                            "Your network speed might be too fast for the current timeout.\nException:" +
                            ex.Message);
                    }
                });
            }
        }

        [Fact]
        public async Task LocalFileUploadTest2()
        {
            var stream = Stream.Null;
            var site = await SiteAsync;
            await Assert.ThrowsAsync<OperationFailedException>(() => FilePage.UploadAsync(site, stream,
                "File:Null.png", "This upload should have failed.", false));
        }

        [SkippableFact]
        public async Task ExternalFileUploadTest1()
        {
            const string SourceUrl = "https://upload.wikimedia.org/wikipedia/commons/5/55/8-cell-simple.gif";
            const string Description =
                @"A 3D projection of an 8-cell performing a simple rotation about a plane which bisects the figure from front-left to back-right and top to bottom.

This work has been released into the public domain by its author, JasonHise at English Wikipedia. This applies worldwide.

In some countries this may not be legally possible; if so:

JasonHise grants anyone the right to use this work for any purpose, without any conditions, unless such conditions are required by law.";
            const string ReuploadSuffix = "\n\nReuploaded.";
            const string FileName = "File:8-cell-simple.gif";
            var site = await CreateIsolatedWikiSiteAsync(CredentialManager.DirtyTestsEntryPointUrl);
            // Allow for more time to wait.
            site.WikiClient.Timeout = TimeSpan.FromSeconds(30);
            try
            {
                await FilePage.UploadAsync(site, SourceUrl, FileName, Description, false);
            }
            catch (UploadException ex)
            {
                // Client should rarely be checking like this
                // Usually we should notify the user.
                if (ex.UploadResult.Warnings[0].Key == "exists")
                    // Just re-upload
                    await FilePage.UploadAsync(site, ex.UploadResult, FileName, Description + ReuploadSuffix, true);
                else
                    throw;
            }
            catch (OperationFailedException ex)
            {
                Skip.If(ex.ErrorCode == "copyuploadbaddomain", ex.ErrorMessage);
                throw;
            }
        }

    }
}
