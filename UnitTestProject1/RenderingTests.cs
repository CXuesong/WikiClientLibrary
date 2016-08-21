using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class RenderingTests
    {
        private static Site WpTestSite;

        [ClassInitialize]
        public static void OnClassInitializing(TestContext context)
        {
            // Prepare test environment.
            WpTestSite = CreateWikiSite(EntryPointWikipediaTest2);
            //CredentialManager.Login(WpTestSite);
        }

        [ClassCleanup]
        public static void OnClassCleanup()
        {
            //CredentialManager.Logout(WpTestSite);
        }

        [TestMethod]
        public void ParsePageTest()
        {
            var site = WpTestSite;
            var p = AwaitSync(site.ParsePage("Main Page", true));
            ShallowTrace(p);
        }
    }
}
