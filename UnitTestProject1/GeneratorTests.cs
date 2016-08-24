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

        private void AssertTitlesDistinct(IReadOnlyCollection<Page> pages)
        {
            var distinctTitles = pages.Select(p => p.Title).Distinct().Count();
            Assert.AreEqual(pages.Count, distinctTitles);
        }

        private void TracePages(IReadOnlyCollection<Page> pages)
        {
            const string lineFormat = "{0,-20} {1,10} {2,10} {3,10} {4,10}";
            Trace.WriteLine(pages.Count + " pages.");
            Trace.WriteLine(string.Format(lineFormat, "Title", "Length", "Last Revision", "Last Touched", "Children"));
            foreach (var page in pages)
            {
                var childrenField = "";
                var cat = page as Category;
                if (cat != null) childrenField = $"{cat.MembersCount}(sub:{cat.SubcategoriesCount})";
                Trace.WriteLine(string.Format(lineFormat, page.Title, page.ContentLength, page.LastRevisionId,
                    page.LastTouched, childrenField));
                if (page.Content != null)
                    Trace.WriteLine(page.Content.Length > 100 ? page.Content.Substring(0, 100) + "..." : page.Content);
            }
        }

        [TestMethod]
        public void WpAllPagesGeneratorTest1()
        {
            var site = WpTestSite;
            var generator = new AllPagesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WpAllPagesGeneratorTest2()
        {
            var site = WpTestSite;
            var generator = new AllPagesGenerator(site) {StartTitle = "W", PagingSize = 20};
            var pages = generator.EnumPages(true).Take(100).ToList();
            TracePages(pages);
            Assert.IsTrue(pages[0].Title[0] == 'W');
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WikiaAllPagesGeneratorTest()
        {
            var site = WikiaTestSite;
            var generator = new AllPagesGenerator(site) {NamespaceId = BuiltInNamespaces.Template};
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WpAllCategoriesGeneratorTest()
        {
            var site = WpTestSite;
            var generator = new AllCategoriesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            generator = new AllCategoriesGenerator(site) {StartTitle = "C", PagingSize = 20};
            pages = generator.EnumPages(true).Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WikiaAllCategoriesGeneratorTest()
        {
            var site = WikiaTestSite;
            var generator = new AllCategoriesGenerator(site);
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WpCategoryMembersGeneratorTest()
        {
            var site = WpTestSite;
            var cat = new Category(site, "Category:Template documentation pages‏‎");
            AwaitSync(cat.RefreshAsync());
            Trace.WriteLine(cat);
            var generator = new CategoryMembersGenerator(cat) {PagingSize = 50};
            var pages = generator.EnumPages().ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            Assert.AreEqual(cat.MembersCount, pages.Count);
        }

        [TestMethod]
        public void WikiaCategoryMembersGeneratorTest()
        {
            var site = WikiaTestSite;
            var cat = new Category(site, "Category:BlogListingPage‏‎‏‎");
            AwaitSync(cat.RefreshAsync());
            Trace.WriteLine(cat);
            var generator = new CategoryMembersGenerator(cat) {PagingSize = 50};
            var pages = generator.EnumPages().ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            Assert.AreEqual(cat.MembersCount, pages.Count);
        }


        [TestMethod]
        public void WpRecentChangesGeneratorTest1()
        {
            var site = WpTestSite;
            var generator = new RecentChangesGenerator(site) {LastRevisionsOnly = true, PagingSize = 20};
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        //ISSUE
        // There's something wrong with wikia's continuation,
        // when using RecentChanges as generator rather than a
        // list to query. The continuation just failed to make effects
        // and the continued page of results just like the previous page.
        public void WikiaRecentChangesGeneratorTest1()
        {
            var site = CreateWikiSite("http://warriors.wikia.com/api.php");
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                PagingSize = 100,
            };
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }
    }
}
