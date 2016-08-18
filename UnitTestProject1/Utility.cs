using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary;
using System.Diagnostics;
using WikiClientLibrary.Client;

namespace UnitTestProject1
{
    internal static class Utility
    {
        public static WikiClient CreateWikiClient()
        {
            var client = new WikiClient
            {
                Logger = new TraceLogger(),
                EndPointUrl = "https://test2.wikipedia.org/w/api.php"
            };
            return client;
        }

        private class TraceLogger : ILogger
        {
            public void Trace(string message)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }

            public void Warn(string message)
            {
                System.Diagnostics.Trace.TraceWarning(message);
            }

            public void Error(Exception exception, string message)
            {
                System.Diagnostics.Trace.TraceError("{0}, {1}", message, exception.ToString());
            }
        }
    }
}
