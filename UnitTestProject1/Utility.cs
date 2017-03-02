using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary.Client;

namespace UnitTestProject1
{
    internal static class Utility
    {
        public const string EntryPointWikipediaTest2 = "https://test2.wikipedia.org/w/api.php";

        /// <summary>
        /// WMF beta test site. We only apply the tests that cannot be performed in test2.wikipedia.org (e.g. Flow boards).
        /// </summary>
        public const string EntryPointWikipediaBetaEn = "https://en.wikipedia.beta.wmflabs.org/w/api.php";

        /// <summary>
        /// This is NOT a test site so do not make modifications to the site.
        /// </summary>
        public const string EntryWikipediaLzh = "https://zh-classical.wikipedia.org/w/api.php";

        // TODO This is a rather unofficial test site. Replace it in the future.
        public const string EntryPointWikiaTest = "https://mediawiki119.wikia.com/api.php";

        /// <summary>
        /// Asserts that modifications to wiki site can be done in unit tests.
        /// </summary>
        public static void AssertModify()
        {
#if DRY_RUN
            Assert.Inconclusive("Remove #define DRY_RUN to perform edit tests.");
#endif
        }

        public static WikiClient CreateWikiClient()
        {
            var client = new WikiClient
            {
                Logger = new TraceLogger(),
                Timeout = TimeSpan.FromSeconds(20),
                ThrottleTime = TimeSpan.FromSeconds(1),
                RetryDelay = TimeSpan.FromSeconds(5),
                ClientUserAgent = "UnitTest/1.0 (.NET CLR " + Environment.Version + ")",
            };
            return client;
        }

        public static Site CreateWikiSite(string entryPointUrl, bool login = false)
        {
            var client = CreateWikiClient();
            var options = new SiteOptions(entryPointUrl)
            {
                AccountAssertion = AccountAssertionBehavior.AssertAll
            };
            var site = AwaitSync(Site.CreateAsync(client, options));
            site.Logger = new TraceLogger();
            if (login) CredentialManager.Login(site);
            return site;
        }

        private class TraceLogger : ILogger
        {
            public void Trace(string message)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }

            public void Info(string message)
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
        /// <remarks>This method is specially for unit tests. You should use async idiom in your code.</remarks>
        public static T AwaitSync<T>(Task<T> task)
        {
            return AwaitSync<T>((Task) task);
        }

        /// <summary>
        /// Runs Task synchronously, and returns the result.
        /// </summary>
        /// <remarks>This method is specially for unit tests. You should use async idiom in your code.</remarks>
        public static void AwaitSync(Task task)
        {
            AwaitSync<bool>(task);
        }

        private static string DumpObject(object obj, int indention, int maxDepth)
        {
            if (obj == null) return "null";
            var sb = new StringBuilder();
            if (obj.GetType().GetMethod("ToString", Type.EmptyTypes).DeclaringType
                == typeof (object))
            {
                sb.Append('{');
                sb.Append(obj.GetType().Name);
                sb.Append('}');
            }
            else
            {
                sb.Append(obj);
            }
            if (maxDepth < 1 || obj is ValueType || obj is string || obj is Uri)
            {
                var s = sb.ToString();
                if (s.Length > 1024) s = s.Substring(0, 1024) + "...";
                return s;
            }
            foreach (var p in obj.GetType().GetProperties())
            {
                if (p.GetIndexParameters().Length > 0) continue;
                sb.AppendLine();
                sb.Append(' ', indention*2);
                sb.Append(p.Name);
                sb.Append(" = ");
                var value = p.GetValue(obj);
                sb.Append(DumpObject(value, indention + 1, maxDepth - 1));
            }
            var dict = obj as IDictionary;
            var enu = obj as IEnumerable;
            if (dict != null)
            {
                foreach (DictionaryEntry p in dict)
                {
                    sb.AppendLine();
                    sb.Append(' ', indention*2);
                    sb.AppendFormat("[{0}] = ", p.Key);
                    sb.Append(DumpObject(p.Value, indention + 1, maxDepth - 1));
                }
            }
            else if (enu != null)
            {
                foreach (var i in enu)
                {
                    sb.Append(' ', indention*2);
                    sb.AppendLine();
                    sb.Append(DumpObject(i, indention + 1, maxDepth - 1));
                }
            }
            return sb.ToString();
        }

        public static string DumpObject(object obj, int maxDepth)
        {
            return DumpObject(obj, 0, maxDepth);
        }

        public static void ShallowTrace(object obj, int depth = 2)
        {
            Trace.WriteLine(DumpObject(obj, depth));
        }

        public static void AssertLoggedIn(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (!site.UserInfo.IsUser) Assert.Inconclusive($"User {site.UserInfo} has not logged into {site}.");
        }

        public static Tuple<Stream, string> GetDemoImage()
        {
            // Load DemoImage.jpg
            var content = typeof (Utility).Assembly.GetManifestResourceStream("UnitTestProject1.DemoImage.jpg");
            using (var r = new StreamReader(typeof (Utility).Assembly.
                GetManifestResourceStream("UnitTestProject1.DemoImage.txt")))
            {
                var desc = r.ReadToEnd();
                return Tuple.Create(content, desc);
            }
        }
    }
}
