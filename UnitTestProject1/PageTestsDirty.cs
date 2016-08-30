// Enables the following conditional switch in the project options
// to prevent test cases from making any edits.
//          DRY_RUN

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static UnitTestProject1.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;

namespace UnitTestProject1
{
    /// <summary>
    /// The tests in this class requires a site administrator (i.e. sysop) account.
    /// </summary>
    [TestClass]
    public class PageTestsDirty
    {
        private const string SummaryPrefix = "WikiClientLibrary test. ";

        // The following pages will be created.
        private const string TestPage1Title = "WCL test page 1";

        private const string TestPage11Title = "WCL test page 1/1";

        private const string TestPage2Title = "WCL test page 2";

        // The following pages will NOT be created at first.
        private const string TestPage12Title = "WCL test page 1/2";

        private static Site site;

        private static Page GetOrCreatePage(Site site, string title)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var page = new Page(site, title);
            AwaitSync(page.RefreshAsync());
            if (!page.Exists)
            {
                Trace.WriteLine("Creating page: " + page);
                page.Content = $@"<big>This is a test page for '''WikiClientLibrary'''.</big>

This page is created by an automated program for unit test purposes.

If you see this page '''OUTSIDE''' a test wiki site,
maybe you should consider viewing the history of the page,
and find out who created the page accidentally.

The original title of the page is '''{title}'''.

== See also ==
* [[Special:PrefixIndex/{title}/|Subpages]]
";
                AwaitSync(page.UpdateContentAsync(SummaryPrefix + "Create test page for unit tests."));
            }
            return page;
        }

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {
            AssertModify(); // We're doing dirty work in this calss.
            // Prepare test environment.
            site = CreateWikiSite(CredentialManager.DirtyTestsEntryPointUrl);
            CredentialManager.Login(site);
            site.UserInfo.AssertInGroup("sysop");
            GetOrCreatePage(site, TestPage1Title);
            GetOrCreatePage(site, TestPage11Title);
            GetOrCreatePage(site, TestPage2Title);
        }

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            CredentialManager.Logout(site);
        }

        [TestMethod]
        public void PageMoveAndDeleteTest1()
        {
            var page1 = new Page(site, TestPage11Title);
            var page2 = new Page(site, TestPage12Title);
            Trace.WriteLine("Deleted:" + AwaitSync(page2.DeleteAsync(SummaryPrefix + "Delete the move destination.")));
            AwaitSync(page1.MoveAsync(TestPage12Title, SummaryPrefix + "Move a page.", PageMovingOptions.IgnoreWarnings));
            AwaitSync(page2.DeleteAsync(SummaryPrefix + "Delete the moved page."));
        }

        [TestMethod]
        public void LocalFileUploadTest1()
        {
            const string FileName = "File:Test image.jpg";
            const string FileSHA1 = "81ED69FA2C2BDEEBBA277C326D1AAC9E0E57B346";
            const string ReuploadSuffix = "\n\nReuploaded.";
            var file = GetDemoImage();
            try
            {
                AwaitSync(FilePage.UploadAsync(site, file.Item1, FileName, file.Item2, false));
            }
            catch (UploadException ex)
            {
                // Client should rarely be checking like this
                // Usually we should notify the user.
                if (ex.UploadResult.Warnings[0].Key == "exists")
                    // Just re-upload
                    AwaitSync(FilePage.UploadAsync(site, ex.UploadResult, FileName, file.Item2 + ReuploadSuffix, true));
                else
                    throw;
            }
            var fp = new FilePage(site, FileName);
            AwaitSync(fp.RefreshAsync());
            ShallowTrace(fp);
            Assert.IsTrue(fp.Exists);
            Assert.AreEqual(FileSHA1, fp.LastFileRevision.Sha1.ToUpperInvariant());
        }

        [TestMethod]
        [ExpectedException(typeof(OperationFailedException))]
        public void LocalFileUploadTest2()
        {
            var stream = Stream.Null;
            AwaitSync(FilePage.UploadAsync(site, stream, "File:Null.png", "This upload should have failed.", false));
        }

        [TestMethod]
        public void ExternalFileUploadTest1()
        {
            const string SourceUrl = "https://upload.wikimedia.org/wikipedia/commons/5/55/8-cell-simple.gif";
            const string Description =
                @"A 3D projection of an 8-cell performing a simple rotation about a plane which bisects the figure from front-left to back-right and top to bottom.

This work has been released into the public domain by its author, JasonHise at English Wikipedia. This applies worldwide.

In some countries this may not be legally possible; if so:

JasonHise grants anyone the right to use this work for any purpose, without any conditions, unless such conditions are required by law.";
            const string ReuploadSuffix = "\n\nReuploaded.";
            const string FileName = "File:8-cell-simple.gif";
            var timeout = site.WikiClient.Timeout;
            site.WikiClient.Timeout = TimeSpan.FromSeconds(30);
            try
            {
                AwaitSync(FilePage.UploadAsync(site, SourceUrl, FileName, Description, false));
            }
            catch (UploadException ex)
            {
                // Client should rarely be checking like this
                // Usually we should notify the user.
                if (ex.UploadResult.Warnings[0].Key == "exists")
                    // Just re-upload
                    AwaitSync(FilePage.UploadAsync(site, ex.UploadResult, FileName, Description + ReuploadSuffix, true));
                else
                    throw;
            }
            finally
            {
                site.WikiClient.Timeout = timeout;
            }
        }
    }
}
