using System.Text.Json;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia;

internal static class Utility
{

    public static readonly JsonSerializerOptions WikiaApiJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new WikiReadOnlyDictionaryConverterFactory() },
    };

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

}
