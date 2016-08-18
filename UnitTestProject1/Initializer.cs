using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Serialization;

namespace UnitTestProject1
{
    [TestClass]
    public class Initializer
    {
        [AssemblyInitialize]
        public static void OnAssemblyInitializing(TestContext context)
        {
            // Redirects all the tracing message to Trace.
        }
    }
}
