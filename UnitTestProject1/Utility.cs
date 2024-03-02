using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;

namespace WikiClientLibrary.Tests.UnitTestProject1;

internal static class Utility
{

    private static string DumpObject(object? obj, int indention, int maxDepth)
    {
        if (obj == null) return "null";
        if ("".Equals(obj)) return "<String.Empty>";
        var sb = new StringBuilder();
        if (obj.GetType().GetMethod("ToString", Type.EmptyTypes)!.DeclaringType == typeof(object))
        {
            sb.Append('{');
            sb.Append(obj.GetType().Name);
            sb.Append('}');
        }
        else if (obj is DateTime || obj is DateTimeOffset)
        {
            sb.AppendFormat("{0:O}", obj);
        }
        else
        {
            sb.Append(obj);
        }
        if (maxDepth < 1 || obj.GetType().IsPrimitive
                         || obj is string || obj is Uri || obj is DateTime || obj is DateTimeOffset)
        {
            var s = sb.ToString();
            if (s.Length > 1024) s = s.Substring(0, 1024) + "...";
            return s;
        }
        if (obj is ICollection collection)
        {
            sb.AppendLine();
            sb.Append(' ', indention * 2);
            sb.AppendFormat("Count = {0}", collection.Count);
        }
        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry? p in dict)
            {
                sb.AppendLine();
                sb.Append(' ', indention * 2);
                sb.AppendFormat("[{0}] = ", p!.Value.Key);
                sb.Append(DumpObject(p.Value, indention + 1, maxDepth - 1));
            }
        }
        else if (obj is IEnumerable enu)
        {
            var index = 0;
            foreach (var i in enu)
            {
                sb.AppendLine();
                sb.Append(' ', indention * 2);
                sb.AppendFormat("[{0}] = ", index);
                sb.Append(DumpObject(i, indention + 1, maxDepth - 1));
                index++;
            }
        }
        else
        {
            foreach (var p in obj.GetType().GetProperties()
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

    public static string DumpObject(object? obj, int maxDepth)
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
        var assembly = typeof(Utility).Assembly;
        var content = assembly.GetManifestResourceStream($"WikiClientLibrary.Tests.UnitTestProject1.DemoImages.{imageName}.jpg");
        if (content == null) throw new ArgumentException("Invalid imageName.");
        using var r = new StreamReader(
            assembly.GetManifestResourceStream($"WikiClientLibrary.Tests.UnitTestProject1.DemoImages.{imageName}.txt")!);
        var desc = r.ReadToEnd();
        return new DemoFileInfo(content, desc);
    }

    private static readonly Random random = new Random();

    public static string RandomTitleString(int length = 8)
    {
        var sb = new StringBuilder();
        lock (random)
        {
            for (int i = 0; i < length; i++)
            {
                var nextDigit = random.Next(0, 36);
                if (nextDigit < 10)
                    sb.Append((char)('0' + nextDigit));
                else
                    sb.Append((char)('a' - 10 + nextDigit));
            }
        }
        return sb.ToString();
    }

    public static void AssertTitlesDistinct(IList<WikiPage> pages)
    {
        var distinctTitles = pages.GroupBy(p => p.Title);
        foreach (var group in distinctTitles)
        {
            var count = group.Count();
            var indices = string.Join(',', group.Select(pages.IndexOf));
            Assert.True(count == 1, $"Page instance [[{group.Key}]] is not unique. Found {count} instances with indices {indices}.");
        }
    }

    /// <summary>
    /// Equivalent to <see cref="Utility.AssertNotNull"/>, but with NRT annotation.
    /// </summary>
    public static void AssertNotNull([NotNull] object? obj)
    {
        // TODO Remove this function when xunit v3 comes out
        // See https://github.com/xunit/assert.xunit/blob/main/NullAsserts.cs
        // See https://github.com/xunit/xunit/issues/2133
        if (obj is null)
        {
            Assert.NotNull(obj);
            Debug.Fail("Assertion should already fail. Should not reach here.");
        }
    }

    /// <summary>
    /// A rather simple method (but working in most cases) to parse all the section from the wikitext.
    /// </summary>
    public static List<ParsedSectionInfo> WikitextParseSections(string content)
    {
        var matches = Regex.Matches(content, @"^(?'LEFT'={1,8})(?'TITLE'[^=].*?)\k<LEFT>\s*$",
            RegexOptions.Multiline);
        var parsedSections = new List<ParsedSectionInfo>();
        string? lastSectionTitle = null;
        var lastSectionStartsAt = 0;
        foreach (var match in matches.Cast<Match>())
        {
            var sectionStartsAt = match.Index;
            parsedSections.Add(new ParsedSectionInfo(lastSectionTitle, content[lastSectionStartsAt..sectionStartsAt]));
            lastSectionTitle = match.Groups["TITLE"].Value.Trim();
            lastSectionStartsAt = sectionStartsAt;
        }
        parsedSections.Add(new ParsedSectionInfo(lastSectionTitle, content[lastSectionStartsAt..]));
        return parsedSections;
    }

}

/// <param name="Title">Section title.</param>
/// <param name="Content">Section content, including the title part.</param>
public record ParsedSectionInfo(string? Title, string Content);

internal struct DemoFileInfo
{
    public DemoFileInfo(Stream contentStream, string? description)
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

    public string? Description { get; }

    public string? Sha1 { get; }

}