using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class WebClientTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var client = Utility.CreateWikiClient();
            var json = client.GetJsonAsync(new {action = "query", meta = "siteinfo"}).Result;
            Trace.WriteLine(json);
        }
    }
}
