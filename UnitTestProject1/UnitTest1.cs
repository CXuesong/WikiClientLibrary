using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using WikiClientLibrary;

namespace UnitTestProject1
{
    [TestClass]
    public class SiteTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var client = Utility.CreateWikiClient();
            var site = new Site(client);
            site.RefreshAsync().Wait();
            Trace.WriteLine(site);
            Trace.WriteLine(JObject.FromObject(site));
            Assert.AreEqual(site.Name, "Wikipedia");
        }
    }
}
