using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using WikiClientLibrary.Client;
using Newtonsoft.Json.Linq;

namespace UnitTestProject1
{
    internal static class Utility
    {
        public const string EntryPointWikipediaTest2 = "https://test2.wikipedia.org/w/api.php";
        // TODO This is a rather unofficial test site. Replace it in the future.
        public const string EntryPointWikiaTest = "https://mediawiki119.wikia.com/api.php";

        public static WikiClient CreateWikiClient(string entryPointUrl)
        {
            var client = new WikiClient
            {
                Logger = new TraceLogger(),
                EndPointUrl = entryPointUrl,
            };
            return client;
        }

        public static Site CreateWikiSite(string entryPointUrl)
        {
            var client = CreateWikiClient(entryPointUrl);
            var site = AwaitSync(Site.GetAsync(client));
            return site;
        }

        private class TraceLogger : ILogger
        {
            public void Trace(string message)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }

            public void Warn(string message)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }

            public void Error(Exception exception, string message)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("{0}, {1}", message, exception));
            }
        }

        /// <summary>
        /// Runs Task synchronously, and returns the result.
        /// </summary>
        private static T AwaitSync<T>(Task task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            try
            {
                task.Wait();
            }
            catch (AggregateException ex)
            {
                // Expand exception
                if (ex.InnerExceptions.Count == 1)
                    ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
            var tt = task as Task<T>;
            return tt == null ? default(T) : tt.Result;
        }

        /// <summary>
        /// Runs Task synchronously, and returns the result.
        /// </summary>
        public static T AwaitSync<T>(Task<T> task)
        {
            return AwaitSync<T>((Task) task);
        }

        /// <summary>
        /// Runs Task synchronously, and returns the result.
        /// </summary>
        public static void AwaitSync(Task task)
        {
            AwaitSync<bool>(task);
        }

        public static string DumpSite(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var sb = new StringBuilder();
            sb.AppendLine("Info");
            sb.AppendLine(JObject.FromObject(site.Info).ToString());
            sb.AppendLine();
            sb.AppendLine("Namespaces");
            foreach (var ns in site.Namespaces.Values.OrderBy(n => n.Id))
            {
                sb.AppendLine(ns.ToString());
            }
            return sb.ToString();
        }

        public static void TraceSite(Site site)
        {
            Trace.WriteLine(DumpSite(site));
        }
    }
}
