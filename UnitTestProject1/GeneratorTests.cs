using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class GeneratorTests
    {
        private static Site WpTestSite;

        private static Site WikiaTestSite;

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {
            // Prepare test environment.
            WpTestSite = CreateWikiSite(EntryPointWikipediaTest2);
            //CredentialManager.Login(WpTestSite);
            WikiaTestSite = CreateWikiSite(EntryPointWikiaTest);
            //CredentialManager.Login(WikiaTestSite);
        }

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            //CredentialManager.Logout(WpTestSite);
            //CredentialManager.Logout(WikiaTestSite);
        }

        private void TracePages(IReadOnlyCollection<Page> pages)
        {
            const string lineFormat = "{0,-20} {1,10} {2,10} {3,10}";
            Trace.WriteLine(pages.Count + " pages.");
            Trace.WriteLine(string.Format(lineFormat, "Title", "Length", "Last Revision", "Last Touched"));
            foreach (var page in pages)
            {
                Trace.WriteLine(string.Format(lineFormat, page.Title, page.ContentLength, page.LastRevisionId,
                    page.LastTouched));
                if (page.Content != null)
                    Trace.WriteLine(page.Content.Length > 100 ? page.Content.Substring(100) + "..." : page.Content);
            }
        }

        [TestMethod]
        public void WpAllPagesGeneratorTest()
        {
            var site = WpTestSite;
            var generator = new AllPagesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            generator = new AllPagesGenerator(site) {StartTitle = "C", PagingSize = 20};
            pages = generator.EnumPages(true).Take(100).ToList();
            TracePages(pages);
        }

        [TestMethod]
        public void WikiaAllPagesGeneratorTest()
        {
            var site = WikiaTestSite;
            var generator = new AllPagesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
        }
    }
}
