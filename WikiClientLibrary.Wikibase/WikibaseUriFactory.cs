using System.Collections.Concurrent;
using System.Diagnostics;

namespace WikiClientLibrary.Wikibase;

// TODO: Take over the URI deserialization to leverage cache in WikibaseUriFactory.

/// <summary>
/// A static class provides functionality for caching <see cref="Uri"/> instances.
/// </summary>
public static class WikibaseUriFactory
{

    private static readonly ConcurrentDictionary<string, WeakReference<Uri>> cacheDict = new();

    private static int nextTrimTrigger = 32;

    /// <summary>
    /// Gets an instance of <see cref="Uri"/> by absolute URI string.
    /// </summary>
    /// <param name="uri">absolute URI of the entity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">The provided URI string is not a valid absolute URI.</exception>
    public static Uri Get(string uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        Uri? inst = null;
        // Fast route
        if (cacheDict.TryGetValue(uri, out var r) && r.TryGetTarget(out inst))
            return inst;
        // Slow route
        cacheDict.AddOrUpdate(uri,
            u => new WeakReference<Uri>(inst = new Uri(u)),
            (u, r0) =>
            {
                if (!r0.TryGetTarget(out inst))
                {
                    inst = new Uri(u);
                    return new WeakReference<Uri>(inst);
                }
                return r0;
            });
        var c = cacheDict.Count;
        if (c >= nextTrimTrigger) TrimExcess();
        Debug.Assert(inst != null);
        return inst;
    }

    private static void TrimExcess()
    {
        foreach (var p in cacheDict)
        {
            if (!p.Value.TryGetTarget(out _)) cacheDict.TryRemove(p.Key, out _);
        }
        var c = cacheDict.Count;
        Volatile.Write(ref nextTrimTrigger, c > 0x3FFFFFFF ? int.MaxValue : c * 2);
    }

    /// <summary>
    /// Clears all the cached <see cref="Uri"/> instances.
    /// </summary>
    public static void ClearCache()
    {
        cacheDict.Clear();
        nextTrimTrigger = 32;
    }

}
