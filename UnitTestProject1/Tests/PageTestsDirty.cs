// To prevent test cases from making any edits, See WikiSiteTestsBase.cs.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Files;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{
    /// <summary>
    /// The tests in this class requires a site administrator (i.e. sysop) account.
    /// </summary>
    public class PageTestsDirty : WikiSiteTestsBase
    {
        private const string SummaryPrefix = "WikiClientLibrary test. ";

        // The following pages will be created.
        private const string TestPage1Title = "WCL test page 1";

        private const string TestPage11Title = "WCL test page 1/1";

        private const string TestPage2Title = "WCL test page 2";

        // The following pages will NOT be created at first.
        private const string TestPage12Title = "WCL test page 1/2";

        public Task<WikiSite> SiteAsync
        {
            get
            {
                if (string.IsNullOrEmpty(CredentialManager.DirtyTestsEntryPointUrl))
                    throw new NotSupportedException();
                return GetWikiSiteAsync(CredentialManager.DirtyTestsEntryPointUrl);
            }
        }

        /// <inheritdoc />
        public PageTestsDirty(ITestOutputHelper output) : base(output)
        {
            if (CredentialManager.DirtyTestsEntryPointUrl == null)
                throw new SkipException(
                    "You need to specify CredentialManager.DirtyTestsEntryPointUrl before running this group of tests.");
            SiteNeedsLogin(CredentialManager.DirtyTestsEntryPointUrl);
            SiteNeedsLogin(Endpoints.WikiaTest);
        }

        private async Task<WikiPage> GetOrCreatePage(WikiSite site, string title)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var page = new WikiPage(site, title);
            await page.RefreshAsync();
            if (!page.Exists)
            {
                WriteOutput("Creating page: {0}", page);
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

        [SkippableFact]
        public async Task PageMoveAndDeleteTest1()
        {
            var site = await SiteAsync;
            Skip.IfNot(site.AccountInfo.IsInGroup(UserGroups.SysOp),
                "The user is not in sysop group and cannot delete the pages.");
            var page1 = await GetOrCreatePage(site, TestPage11Title);
            var page2 = new WikiPage(site, TestPage12Title);
            WriteOutput("Deleted: {0}", await page2.DeleteAsync(SummaryPrefix + "Delete the move destination."));
            await page1.MoveAsync(TestPage12Title, SummaryPrefix + "Move a page.", PageMovingOptions.IgnoreWarnings);
            WriteOutput("Moved {0} to {1}.", page1, page2);
            await page2.DeleteAsync(SummaryPrefix + "Delete the moved page.");
        }

        [SkippableTheory]
        [InlineData("File:Test image {0}.jpg", "1")]
        [InlineData("File:Test image {0}.jpg", "2")]
        [InlineData("Test image {0}.jpg", "1")]
        public async Task LocalFileUploadTest1(string fileName, string imageName)
        {
            const string ReuploadSuffix = "\n\nReuploaded.";
            var site = await SiteAsync;
            var file = Utility.GetDemoImage(imageName);
            fileName = string.Format(fileName, Utility.RandomTitleString());
            var result = await site.UploadAsync(fileName, new StreamUploadSource(file.ContentStream), file.Description, false);
            ShallowTrace(result);
            if (result.ResultCode == UploadResultCode.Warning)
            {
                // Usually we should notify the user, then perform the re-upload ignoring the warning.
                try
                {
                    // Uploaded file is a duplicate of `fileName`, or duplicate content detected.
                    Assert.Equal(UploadResultCode.Warning, result.ResultCode);
                    Assert.True(result.Warnings.TitleExists || result.Warnings.DuplicateTitles != null);
                    WriteOutput("Title exists.");
                    // Sometimes there is backend stash error. We may ignore it for now.
                    if (result.StashErrors.Any(e => e.Code == "uploadstash-exception"))
                    {
                        Skip.If(string.IsNullOrEmpty(result.FileKey), "Stash error: " + string.Join(';', result.StashErrors));
                    }
                    Utility.AssertNotNull(result.FileKey);
                    result = await site.UploadAsync(fileName,
                        new FileKeyUploadSource(result.FileKey), file.Description + ReuploadSuffix, true);
                    ShallowTrace(result);
                    Assert.Equal(UploadResultCode.Success, result.ResultCode);
                }
                catch (OperationFailedException ex) when (ex.ErrorCode == "fileexists-no-change")
                {
                    WriteOutput(ex.Message);
                }
            }
            else
            {
                Assert.Equal(UploadResultCode.Success, result.ResultCode);
            }
            var page = new WikiPage(site, fileName, BuiltInNamespaces.File);
            await page.RefreshAsync();
            ShallowTrace(page);
            Assert.True(page.Exists);
            Assert.Equal(file.Sha1, page.LastFileRevision!.Sha1.ToUpperInvariant());
        }

        [Fact]
        public async Task LocalFileUploadRetryTest1()
        {
            // This is to attempt to prevent the following error:
            // backend-fail-alreadyexists: The file "mwstore://local-swift-eqiad/local-public/archive/9/95/20191116051316!Test_image.jpg" already exists.
            var fileName = $"File:Test image {Utility.RandomTitleString()}.jpg";
            var localSite = await CreateIsolatedWikiSiteAsync(CredentialManager.DirtyTestsEntryPointUrl!);
            // Cache the token first so it won't be affected by the short timeout.
            await localSite.GetTokenAsync("edit");
            WriteOutput("Try uploading…");
            // We want to timeout and retry.
            var wikiClient = (WikiClient)localSite.WikiClient;
            wikiClient.Timeout = TimeSpan.FromSeconds(0.1);
            wikiClient.RetryDelay = TimeSpan.FromSeconds(1);
            wikiClient.MaxRetries = 1;
            var buffer = new byte[1024 * 1024 * 2];     // 2MB, I think this size is fairly large.
                                                        // If your connection speed is too fast (>20MB/s) then, well, throttle it plz.
            await using var ms = new MemoryStream(buffer);
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                var result = await localSite.UploadAsync(fileName, new StreamUploadSource(ms),
                    "This is an upload that is destined to fail. Upload timeout test.", false);
                // Usually we should notify the user, then perform the re-upload ignoring the warning.
                Assert.Equal(UploadResultCode.Warning, result.ResultCode);
            });
            // Ensure that the stream has not been disposed in this case.
            ms.Seek(0, SeekOrigin.Begin);
        }

        [Fact]
        public async Task LocalFileUploadTest2()
        {
            var site = await SiteAsync;
            await Assert.ThrowsAsync<OperationFailedException>(() =>
                site.UploadAsync("File:Null.png", new StreamUploadSource(Stream.Null),
                    "This upload should have failed.", false));
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
            const string reuploadSuffix = "\n\nReuploaded.";
            const string fileName = "File:8-cell-simple.gif";
            var site = await CreateIsolatedWikiSiteAsync(CredentialManager.DirtyTestsEntryPointUrl!);
            // Allow for more time to wait.
            ((WikiClient)site.WikiClient).Timeout = TimeSpan.FromSeconds(30);
            try
            {
                var result = await site.UploadAsync(fileName, new ExternalFileUploadSource(SourceUrl), Description, false);
                // Usually we should notify the user, then perform the re-upload ignoring the warning.
                if (result.Warnings.TitleExists)
                    result = await site.UploadAsync(fileName,
                        new FileKeyUploadSource(result.FileKey!), Description + reuploadSuffix, true);
                Assert.Equal(UploadResultCode.Success, result.ResultCode);
            }
            catch (OperationFailedException ex)
            {
                Skip.If(ex.ErrorCode == "copyuploadbaddomain" || ex.ErrorCode == "copyuploaddisabled", ex.ErrorMessage);
                throw;
            }
        }

        [SkippableFact]
        public async Task ChunkedFileUploadTest()
        {
            var site = await SiteAsync;
            var file = Utility.GetDemoImage("1");
            var chunked = new ChunkedUploadSource(site, file.ContentStream) { DefaultChunkSize = 1024 * 4 };
            try
            {
                do
                {
                    var result = await chunked.StashNextChunkAsync();
                    Assert.NotEqual(UploadResultCode.Warning, result.ResultCode);
                    if (result.ResultCode == UploadResultCode.Success)
                    {
                        // As of 2019-10, this does not hold.
                        // Assert.True(result.FileRevision.IsAnonymous);
                        Assert.Equal(file.Sha1, result.FileRevision!.Sha1, StringComparer.OrdinalIgnoreCase);
                    }
                } while (!chunked.IsStashed);
            }
            catch (OperationFailedException ex) when (ex.ErrorCode == "uploadstash-exception")
            {
                Skip.If(ex.ErrorMessage != null && ex.ErrorMessage.Contains("An unknown error occurred in storage backend"),
                    "MW server backend fails: " + ex.Message);
                throw;
            }
            try
            {
                // This is to attempt to prevent the following error:
                // backend-fail-alreadyexists: The file "mwstore://local-swift-eqiad/local-public/archive/9/95/20191116051316!Test_image.jpg" already exists.
                await site.UploadAsync($"Test image {Utility.RandomTitleString()}.jpg", chunked, file.Description, true);
            }
            catch (OperationFailedException ex) when (ex.ErrorCode == "fileexists-no-change")
            {
                // We cannot suppress this error by setting ignoreWarnings = true.
                WriteOutput(ex.Message);
            }
        }

    }
}
