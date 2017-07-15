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
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using Xunit;

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
        public const string EntryPointWikiaTest = "http://mediawiki119.wikia.com/api.php";

        private static string DumpObject(object obj, int indention, int maxDepth)
        {
            if (obj == null) return "null";
            var sb = new StringBuilder();
            if (obj.GetType().GetRuntimeMethod("ToString", Type.EmptyTypes).DeclaringType
                == typeof(object))
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
            foreach (var p in obj.GetType().GetRuntimeProperties().Where(p1 => p1.GetMethod?.IsPublic ?? false))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                sb.AppendLine();
                sb.Append(' ', indention * 2);
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
                    sb.Append(' ', indention * 2);
                    sb.AppendFormat("[{0}] = ", p.Key);
                    sb.Append(DumpObject(p.Value, indention + 1, maxDepth - 1));
                }
            }
            else if (enu != null)
            {
                foreach (var i in enu)
                {
                    sb.Append(' ', indention * 2);
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

        public static void AssertLoggedIn(WikiSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (!site.AccountInfo.IsUser) Utility.Inconclusive($"User {site.AccountInfo} has not logged into {site}.");
        }

        public static Tuple<Stream, string> GetDemoImage()
        {
            // Load DemoImage.jpg
            var assembly = typeof(Utility).GetTypeInfo().Assembly;
            var content = assembly.GetManifestResourceStream("UnitTestProject1.DemoImage.jpg");
            using (var r = new StreamReader(assembly.GetManifestResourceStream("UnitTestProject1.DemoImage.txt")))
            {
                var desc = r.ReadToEnd();
                return Tuple.Create(content, desc);
            }
        }

        public static void Inconclusive()
        {
            Inconclusive(null);
        }

        public static void Inconclusive(string message)
        {
            throw new Exception("[Inconclusive]" + message);
        }
    }
}
