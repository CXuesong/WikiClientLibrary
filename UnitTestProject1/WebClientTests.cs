using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    [TestClass]
    public class WebClientTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var client = CreateWikiClient();
            var json = AwaitSync(client.GetJsonAsync(EntryPointWikipediaTest2,
                new {action = "query", meta = "siteinfo"},
                CancellationToken.None));
            Trace.WriteLine(json);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidActionException))]
        public void TestMethod2()
        {
            var client = CreateWikiClient();
            var json = AwaitSync(client.GetJsonAsync(EntryPointWikipediaTest2,
                new {action = "invalid_action_test", description = "This is a test case for invalid action parameter."},
                CancellationToken.None));
            Trace.WriteLine(json);
        }
    }
}
