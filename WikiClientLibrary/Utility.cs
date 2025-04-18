﻿using System.Buffers;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Text;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary;

internal static class Utility
{

    /// <summary>
    /// Convert name-value paris to URL query format.
    /// This overload handles <see cref="ExpandoObject"/> as well as anonymous objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key-value pair with null value will be excluded. To specify a key with empty value,
    /// consider using <see cref="string.Empty"/> .
    /// </para>
    /// <para>
    /// For <see cref="bool"/> values, if the value is true, a pair with key and empty value
    /// will be generated; otherwise the whole pair will be excluded. 
    /// </para>
    /// <para>
    /// If <paramref name="values"/> is <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey,TValue}"/>
    /// of strings, the values will be returned with no further processing.
    /// </para>
    /// </remarks>
    public static IEnumerable<KeyValuePair<string, string?>> ToWikiStringValuePairs(object values)
    {
        if (values is IEnumerable<KeyValuePair<string, string?>> pc) return pc;
        return MediaWikiHelper.EnumValues(values)
            .Select(p => new KeyValuePair<string, string?>(p.Key, ToWikiQueryValue(p.Value)));
    }

    public static void MergeFrom<TKey, TValue>(this IDictionary<TKey, TValue> dict, IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var item in items)
            dict[item.Key] = item.Value;
    }

    public static void MergeFrom<TKey, TValue>(this IDictionary<TKey, object?> dict, IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var item in items)
            dict[item.Key] = item.Value;
    }

    /*
    Causes ambiguity with MergeFrom<TKey, TValue>
    public static void MergeFrom<TKey, TValue, TValue2>(this IDictionary<TKey,TValue> dict, IEnumerable<KeyValuePair<TKey, TValue2>> items)
        where TValue2 : TValue
    {
        foreach (var item in items)
            dict[item.Key] = item.Value;
    }
    */

    public static string? ToWikiQueryValue(object? value)
    {
        return value switch
        {
            null => null,
            string _ => (string)value,
            bool b => b ? "" : null,
            AutoWatchBehavior awb => awb switch
            {
                AutoWatchBehavior.Default => "preferences",
                AutoWatchBehavior.None => "nochange",
                AutoWatchBehavior.Watch => "watch",
                AutoWatchBehavior.Unwatch => "unwatch",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
            },
            DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture), // ISO 8601
            IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    /// <summary>
    /// Partitions <see cref="IEnumerable{T}"/> into a sequence of <see cref="IEnumerable{T}"/>,
    /// each child <see cref="IEnumerable{T}"/> having the same length, except the last one.
    /// </summary>
    public static IEnumerable<IReadOnlyCollection<T>> Partition<T>(this IEnumerable<T> source, int partitionSize)
    {
        if (partitionSize <= 0) throw new ArgumentOutOfRangeException(nameof(partitionSize));
        var list = new List<T>(partitionSize);
        foreach (var item in source)
        {
            list.Add(item);
            if (list.Count == partitionSize)
            {
                yield return list;
                list.Clear();
            }
        }
        if (list.Count > 0) yield return list;
    }

    public static string? ToString(this PropertyFilterOption value,
        string? withValue, string? withoutValue, string? allValue = "all")
    {
        return value switch
        {
            PropertyFilterOption.Disable => allValue,
            PropertyFilterOption.WithProperty => withValue,
            PropertyFilterOption.WithoutProperty => withoutValue,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    /// <summary>
    /// A cancellable version of StreamReader.ReadToEndAsync .
    /// </summary>
    public static async Task<string> ReadAllStringAsync(this Stream stream, CancellationToken cancellationToken)
    {
        const int BufferSize = 4 * 1024 * 1024;
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new char[BufferSize];
        var builder = new StringBuilder();
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, BufferSize))
        {
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, BufferSize)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(buffer, 0, count);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return builder.ToString();
    }

    /// <summary>
    /// Normalizes part of title (either namespace name or page title, not both)
    /// to its canonical form.
    /// </summary>
    /// <param name="title">The title to be normalized.</param>
    /// <param name="caseSensitive">Whether the title is case-sensitive.</param>
    /// <returns>
    /// Normalized part of title. The underscores are replaced by spaces,
    /// and when <paramref name="caseSensitive"/> is <c>true</c>, the first letter is
    /// upper-case. Multiple spaces will be replaced with a single space. Lading
    /// and trailing spaces will be removed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
    public static string NormalizeTitlePart(string title, bool caseSensitive)
    {
        // Reference to Pywikibot. page.py, Link class.
        if (title == null) throw new ArgumentNullException(nameof(title));
        if (title.Length == 0) return title;
        var state = 0;
        /*
         STATE
         0      Leading whitespace
         1      In title, after non-whitespace
         2      In title, after whitespace
         */
        var sb = new StringBuilder();
        foreach (var c in title)
        {
            var isWhitespace = c is ' ' or '_';
            // Remove left-to-right and right-to-left markers.
            if (c is '\u200e' or '\u200f') continue;
            switch (state)
            {
                case 0:
                    if (!isWhitespace)
                    {
                        sb.Append(caseSensitive ? c : char.ToUpperInvariant(c));
                        state = 1;
                    }
                    break;
                case 1:
                    if (isWhitespace)
                    {
                        sb.Append(' ');
                        state = 2;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
                case 2:
                    if (!isWhitespace)
                    {
                        sb.Append(c);
                        state = 1;
                    }
                    break;
            }
        }
        if (state == 2)
        {
            // Remove trailing space.
            Debug.Assert(sb[^1] == ' ');
            return sb.ToString(0, sb.Length - 1);
        }
        return sb.ToString();
    }

    public static Task<int> CopyRangeToAsync(this Stream source, Stream destination, int byteCount,
        CancellationToken cancellationToken)
    {
        return CopyRangeToAsync(source, destination, byteCount, 1024 * 4, cancellationToken);
    }

    public static async Task<int> CopyRangeToAsync(this Stream source, Stream destination, int byteCount, int bufferSize,
        CancellationToken cancellationToken)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));
        if (byteCount == 0) return 0;
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var bytesRead = 0;
            while (bytesRead < byteCount)
            {
                var chunkSize = Math.Min(byteCount, bufferSize);
                chunkSize = await source.ReadAsync(buffer.AsMemory(0, chunkSize), cancellationToken);
                if (chunkSize == 0) return bytesRead;
                await destination.WriteAsync(buffer.AsMemory(0, chunkSize), cancellationToken);
                bytesRead += chunkSize;
            }
            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

#if !BCL_FEATURE_KVP_TO_DICTIONARY
    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source)
        where TKey : notnull
    {
        return source.ToDictionary(p => p.Key, p => p.Value);
    }
#endif

}
