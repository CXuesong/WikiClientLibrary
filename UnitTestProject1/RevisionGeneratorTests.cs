using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class RevisionGeneratorTests
    {
        private static readonly Lazy<Site> _WpTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikipediaTest2));
        private static readonly Lazy<Site> _WikiaTestSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikiaTest));

        public static Site WpTestSite => _WpTestSite.Value;

        public static Site WikiaTestSite => _WikiaTestSite.Value;

        [TestMethod]
        public void WpTestEnumRevisionsTest1()
        {
            var site = WpTestSite;
            var page = new Page(site, "Page:Edit_page_for_chrome");
            var revisions = AwaitSync(page.EnumRevisionsAsync(20).Skip(5).Take(5).ToList());
            Assert.AreEqual(5, revisions.Count);
            Assert.IsTrue(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }

        [TestMethod]
        public void WpTestEnumRevisionsTest2()
        {
            var site = WpTestSite;
            // 5,100 revisions in total
            var page = new Page(site, "Page:Edit_page_for_chrome");
            var revisions = AwaitSync(page.EnumRevisionsAsync(2000).Take(2000).ToList());
            Assert.IsTrue(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }


        [TestMethod]
        public void WpTestEnumRevisionsTest3()
        {
            var site = WpTestSite;
            // 5,100 revisions in total
            var page = new Page(site, "Page:Edit_page_for_chrome");
            var t1 = new DateTime(2014, 10, 20, 10, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2014, 10, 22, 10, 0, 0, DateTimeKind.Utc);
            var gen = new RevisionGenerator(page)
            {
                TimeAscending = true,
                StartTime = t1,
                EndTime = t2,
            };
            var revisions = AwaitSync(gen.EnumRevisionsAsync().ToList());
            Assert.IsTrue(revisions.SequenceEqual(revisions.OrderBy(r => r.TimeStamp)));
            Assert.IsTrue(revisions.First().TimeStamp >= t1);
            Assert.IsTrue(revisions.Last().TimeStamp <= t2);
            // This holds on 2016-12-09
            Assert.AreEqual(32, revisions.Count);
            ShallowTrace(revisions);
        }

        [TestMethod]
        public void WikiaEnumRevisionsTest1()
        {
            var site = WikiaTestSite;
            var page = new Page(site, "Project:Sandbox");
            var revisions = AwaitSync(page.EnumRevisionsAsync().Skip(5).Take(5).ToList());
            Assert.AreEqual(5, revisions.Count);
            Assert.IsTrue(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }

        [TestMethod]
        public void WikiaEnumRevisionsTest2()
        {
            var site = WikiaTestSite;
            var page = new Page(site, "Project:Sandbox");
            var revisions = AwaitSync(page.EnumRevisionsAsync().Take(2000).ToList());
            Assert.IsTrue(revisions.SequenceEqual(revisions.OrderByDescending(r => r.TimeStamp)));
            ShallowTrace(revisions);
        }

    }
}
