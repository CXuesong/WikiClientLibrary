using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class RenderingTests
    {
        private static Site WpTestSite, WpLzhSite;

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {
            // Prepare test environment.
            WpTestSite = CreateWikiSite(EntryPointWikipediaTest2);
            WpLzhSite = CreateWikiSite(EntryWikipediaLzh);
            //CredentialManager.Login(WpTestSite);
        }

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            //CredentialManager.Logout(WpTestSite);
        }

        [TestMethod]
        public void WpLzhPageParsingTest1()
        {
            var site = WpLzhSite;
            // 一九五二年
            var result = AwaitSync(site.ParseRevisionAsync(240575));
            ShallowTrace(result);
            Assert.AreEqual(result.Title, "一九五二年");
            Assert.AreEqual(result.DisplayTitle, "一九五二年");
            Assert.IsTrue(result.Content.StartsWith("<p><b>一九五二年</b>，繼<b>"));
            Assert.IsTrue(result.Sections.Any(s => s.Heading == "大事"));
        }

        [TestMethod]
        public void WpTestPageParsingTest1()
        {
            var site = WpTestSite;
            var result = AwaitSync(
                site.ParseContentAsync("{{DISPLAYTITLE:''TITLE''}}\nText '''Text'''\n\n{{PAGENAME}}", "Summary.",
                    "TITLE", ParsingOptions.LimitReport));
            ShallowTrace(result, 3);
            Assert.AreEqual(result.Title, "TITLE");
            Assert.AreEqual(result.DisplayTitle, "<i>TITLE</i>");
            Assert.AreEqual(result.Content.Trim(), "<p>Text <b>Text</b></p>\n<p>TITLE</p>");
            result = AwaitSync(site.ParseContentAsync("{{ambox}}", "Summary.", "TITLE",
                ParsingOptions.LimitReport | ParsingOptions.TranscludedPages));
            ShallowTrace(result, 4);
            Assert.IsTrue(result.TranscludedPages.Any(p => p.Title == "Template:Ambox"));
            Assert.IsTrue((int) result.ParserLimitReports.First(r => r.Name == "limitreport")
                              .Content["expansiondepth"]["value"] > 1);
        }
    }
}
