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
using System.Text.RegularExpressions;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    internal static class Utility
    {
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
            var dict = obj as IDictionary;
            var enu = obj as IEnumerable;
            if (obj is ICollection collection)
            {
                sb.AppendLine();
                sb.Append(' ', indention * 2);
                sb.AppendFormat("Count = {0}", collection.Count);
            }
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
                    sb.AppendLine();
                    sb.Append(' ', indention * 2);
                    sb.Append(DumpObject(i, indention + 1, maxDepth - 1));
                }
            }
            else
            {
                foreach (var p in obj.GetType().GetRuntimeProperties()
                    .Where(p1 => p1.GetMethod?.IsPublic ?? false)
                    .OrderBy(p1 => p1.Name))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    sb.AppendLine();
                    sb.Append(' ', indention * 2);
                    sb.Append(p.Name);
                    sb.Append(" = ");
                    var value = p.GetValue(obj);
                    sb.Append(DumpObject(value, indention + 1, maxDepth - 1));
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
            if (!site.AccountInfo.IsUser) throw new SkipException($"User {site.AccountInfo} has not logged into {site}.");
        }

        public static DemoFileInfo GetDemoImage(string imageName)
        {
            // Load DemoImage.jpg
            var assembly = typeof(Utility).GetTypeInfo().Assembly;
            var content = assembly.GetManifestResourceStream($"UnitTestProject1.DemoImages.{imageName}.jpg");
            if (content == null) throw new ArgumentException("Invalid imageName.");
            using (var r = new StreamReader(assembly.GetManifestResourceStream($"UnitTestProject1.DemoImages.{imageName}.txt")))
            {
                var desc = r.ReadToEnd();
                return new DemoFileInfo(content, desc);
            }
        }
    }

    internal struct DemoFileInfo
    {
        public DemoFileInfo(Stream contentStream, string description)
        {
            ContentStream = contentStream;
            Description = description;
            Sha1 = null;
            if (description != null)
            {
                var match = Regex.Match(description, @"^\s*SHA1:\s*([\d\w]+)", RegexOptions.Multiline);
                Sha1 = match.Groups[1].Value;
            }
        }

        public Stream ContentStream { get; }

        public string Description { get; }

        public string Sha1 { get; }

    }
}
