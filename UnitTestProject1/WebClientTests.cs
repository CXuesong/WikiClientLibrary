using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class WebClientTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var client = CreateWikiClient(EntryPointWikipediaTest2);
            var json = AwaitSync(client.GetJsonAsync(new {action = "query", meta = "siteinfo"}));
            Trace.WriteLine(json);
        }
    }
}
