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
        private static readonly Lazy<Site> _WpTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikipediaTest2));
        private static readonly Lazy<Site> _WikiaTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikiaTest));
        private static readonly Lazy<Site> _WpSite = new Lazy<Site>(() => CreateWikiSite(EntryWikipediaLzh));

        public static Site WpTestSite => _WpTestSite.Value;

        public static Site WikiaTestSite => _WikiaTestSite.Value;

        public static Site WpLzhSite => _WpSite.Value;


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
        public void WpTest2RecentChangesGeneratorTest1()
        {
            var site = WpTestSite;
            var generator = new RecentChangesGenerator(site) {LastRevisionsOnly = true, PagingSize = 20};
            var pages = generator.EnumPages().Take(1000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WpLzhRecentChangesGeneratorTest1()
        {
            var site = WpLzhSite;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                // BotFilter = PropertyFilterOption.WithProperty,
                MinorFilter = PropertyFilterOption.WithProperty,
                AnnonymousFilter = PropertyFilterOption.WithoutProperty,
                TypeFilters = RecentChangesFilterTypes.Create | RecentChangesFilterTypes.Edit,
            };
            var pages = generator.EnumPages(true).Take(100).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
            foreach (var p in pages)
            {
                var flags = p.LastRevision.Flags;
                Assert.IsTrue(flags != RevisionFlags.None);
                Assert.IsFalse(flags.HasFlag(RevisionFlags.Annonymous));
                Assert.IsTrue(flags.HasFlag(RevisionFlags.Minor));
            }
        }

        //ISSUE
        // There's something wrong with wikia's continuation,
        // when using RecentChanges as generator rather than a
        // list to query. The continuation just failed to make effects
        // and the continued page of results just like the previous page.
        [TestMethod]
        public void WikiaRecentChangesGeneratorTest1()
        {
            var site = WikiaTestSite;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
            };
            var pages = generator.EnumPages().Take(2000).ToList();
            TracePages(pages);
            AssertTitlesDistinct(pages);
        }

        [TestMethod]
        public void WpLzhRecentChangesListTest()
        {
            var site = WpLzhSite;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
                BotFilter = PropertyFilterOption.WithProperty,
                MinorFilter = PropertyFilterOption.WithProperty,
            };
            var rc = generator.EnumRecentChanges().Take(2000).ToList();
            ShallowTrace(rc, 1);
            foreach (var p in rc)
            {
                var flags = p.Flags;
                Assert.IsTrue(flags != RevisionFlags.None);
                Assert.IsTrue(flags.HasFlag(RevisionFlags.Bot));
                Assert.IsTrue(flags.HasFlag(RevisionFlags.Minor));
            }
        }

        [TestMethod]
        public void WikiaRecentChangesListTest()
        {
            var site = WikiaTestSite;
            var generator = new RecentChangesGenerator(site)
            {
                LastRevisionsOnly = true,
            };
            var rc = generator.EnumRecentChanges().Take(2000).ToList();
            ShallowTrace(rc, 1);
        }

    }
}
