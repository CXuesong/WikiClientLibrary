using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Sites;

partial class WikiSite
{

    private async Task<JsonArray> FetchMessagesAsync(string messagesExpr, CancellationToken cancellationToken)
    {
        var jresult = await InvokeMediaWikiApiAsync2(
            new MediaWikiFormRequestMessage(new { action = "query", meta = "allmessages", ammessages = messagesExpr, }), cancellationToken);
        return jresult["query"]["allmessages"]!.AsArray();
        //return jresult.ToDictionary(m => , m => (string) m["*"]);
    }

    /// <summary>
    /// Get the content of some or all MediaWiki interface messages.
    /// </summary>
    /// <param name="messages">A sequence of message names.</param>
    /// <returns>
    /// A dictionary of message name - message content pairs.
    /// If some messages cannot be found, the corresponding value will be <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="messages"/> contains <c>null</c> item.
    /// OR one of the <paramref name="messages"/> contains pipe character (|).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Trying to fetch all the messages with "*" input.
    /// </exception>
    public Task<IDictionary<string, string>> GetMessagesAsync(IEnumerable<string> messages)
    {
        return GetMessagesAsync(messages, CancellationToken.None);
    }

    /// <summary>
    /// Get the content of some or all MediaWiki interface messages.
    /// </summary>
    /// <param name="messages">A sequence of message names.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <returns>
    /// A dictionary of message name - message content pairs.
    /// If some messages cannot be found, the corresponding value will be <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="messages"/> contains <c>null</c> item.
    /// OR one of the <paramref name="messages"/> contains pipe character (|).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Trying to fetch all the messages with "*" input.
    /// </exception>
    public async Task<IDictionary<string, string>> GetMessagesAsync(IEnumerable<string> messages,
        CancellationToken cancellationToken)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        cancellationToken.ThrowIfCancellationRequested();
        var exprBuilder = new StringBuilder();
        var result = new Dictionary<string, string>();
        foreach (var m in messages)
        {
            if (m == null) throw new ArgumentException("The sequence contains null item.", nameof(messages));
            if (m.Contains("|"))
                throw new ArgumentException($"The message name \"{m}\" contains pipe character.",
                    nameof(messages));
            if (m == "*") throw new InvalidOperationException("Getting all the messages is deprecated.");
            if (exprBuilder.Length > 0) exprBuilder.Append('|');
            exprBuilder.Append(m);
            var jr = await FetchMessagesAsync(exprBuilder.ToString(), cancellationToken);
            foreach (var entry in jr)
            {
                var name = (string)entry["name"];
                //var nname = (string)entry["normalizedname"];
                // for Wikia, there's no normalizedname
                var message = (string)entry["*"];
                //var missing = entry["missing"] != null;       message will be null
                if (message != null) result[name] = message;
            }
        }
        return result;
    }

    /// <summary>
    /// Get the content of MediaWiki interface message.
    /// </summary>
    /// <param name="message">The message name.</param>
    /// <returns>
    /// The message content. OR <c>null</c> if the messages cannot be found.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> contains pipe character (|).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Trying to fetch all the messages with "*" input.
    /// </exception>
    public Task<string?> GetMessageAsync(string message)
    {
        return GetMessageAsync(message, CancellationToken.None);
    }

    /// <summary>
    /// Get the content of MediaWiki interface message.
    /// </summary>
    /// <param name="message">The message name.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <returns>
    /// The message content. OR <c>null</c> if the messages cannot be found.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> contains pipe character (|).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Trying to fetch all the messages with "*" input.
    /// </exception>
    public async Task<string?> GetMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        var result = await GetMessagesAsync(new[] { message }, cancellationToken);
        return result.Values.FirstOrDefault();
    }

    /// <summary>
    /// Gets the statistical information of the MediaWiki site.
    /// </summary>
    public Task<SiteStatistics> GetStatisticsAsync()
    {
        return GetStatisticsAsync(CancellationToken.None);
    }

    /// <summary>
    /// Gets the statistical information of the MediaWiki site.
    /// </summary>
    public async Task<SiteStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var jobj = await InvokeMediaWikiApiAsync2(
            new MediaWikiFormRequestMessage(new { action = "query", meta = "siteinfo", siprop = "statistics", }), cancellationToken);
        var jstat = jobj["query"]?["statistics"]?.AsObject();
        if (jstat == null) throw new UnexpectedDataException();
        var parsed = jstat.Deserialize<SiteStatistics>(MediaWikiHelper.WikiJsonSerializerOptions);
        return parsed;
    }

    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <returns>Search result.</returns>
    /// <remarks>This overload will allow up to 20 results to be returned, and will not resolve redirects.</remarks>
    public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression)
    {
        return OpenSearchAsync(searchExpression, 20, 0, OpenSearchOptions.None, CancellationToken.None);
    }


    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <param name="options">Other options.</param>
    /// <returns>Search result.</returns>
    /// <remarks>This overload will allow up to 20 results to be returned.</remarks>
    public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, OpenSearchOptions options)
    {
        return OpenSearchAsync(searchExpression, 20, 0, options, CancellationToken.None);
    }

    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.
    /// prior to MW 1.28, this value was capped at 100, regardless of user permissions.</param>
    /// <returns>Search result.</returns>
    /// <remarks>This overload will not resolve redirects.</remarks>
    public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount)
    {
        return OpenSearchAsync(searchExpression, maxCount, 0, OpenSearchOptions.None, CancellationToken.None);
    }

    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
    /// <param name="options">Other options.</param>
    /// <returns>Search result.</returns>
    public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount,
        OpenSearchOptions options)
    {
        return OpenSearchAsync(searchExpression, maxCount, 0, options, CancellationToken.None);
    }

    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
    /// <param name="options">Other options.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <returns>Search result.</returns>
    public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount,
        OpenSearchOptions options, CancellationToken cancellationToken)
    {
        return OpenSearchAsync(searchExpression, maxCount, 0, options, cancellationToken);
    }

    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
    /// <param name="defaultNamespaceId">Default namespace id to search. See <see cref="BuiltInNamespaces"/> for a list of possible namespace ids.</param>
    /// <param name="options">Other options.</param>
    /// <returns>Search result.</returns>
    public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount,
        int defaultNamespaceId, OpenSearchOptions options)
    {
        return OpenSearchAsync(searchExpression, maxCount, defaultNamespaceId, options, CancellationToken.None);
    }

    /// <summary>
    /// Performs an opensearch and get results, often used for search box suggestions.
    /// (MediaWiki 1.25 or OpenSearch extension)
    /// </summary>
    /// <param name="searchExpression">The beginning part of the title to be searched.</param>
    /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
    /// <param name="defaultNamespaceId">Default namespace id to search. See <see cref="BuiltInNamespaces"/> for a list of possible namespace ids.</param>
    /// <param name="options">Other options.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <returns>Search result.</returns>
    public async Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount,
        int defaultNamespaceId, OpenSearchOptions options, CancellationToken cancellationToken)
    {
        /*
[
 "Te",
 [
     "Te",
     "Television",
 ],
 [
     "From other capitalisation: ...",
     "Television or TV is ...",
 ],
 [
     "https://en.wikipedia.org/wiki/Te",
     "https://en.wikipedia.org/wiki/Television",
 ]
]
          */
        if (string.IsNullOrEmpty(searchExpression)) throw new ArgumentNullException(nameof(searchExpression));
        if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
        var jresult = await InvokeMediaWikiApiAsync2(new MediaWikiFormRequestMessage(new
        {
            action = "opensearch",
            @namespace = defaultNamespaceId,
            search = searchExpression,
            limit = maxCount,
            redirects = (options & OpenSearchOptions.ResolveRedirects) == OpenSearchOptions.ResolveRedirects,
        }), cancellationToken);
        var result = new List<OpenSearchResultEntry>();
        var jarray = jresult.AsArray();
        // No result.
        if (jarray.Count <= 1) return result;

        var titles = jarray[1]!.AsArray();
        var descs = jarray.Count > 2 ? jarray[2]!.AsArray() : null;
        var urls = jarray.Count > 3 ? jarray[3]!.AsObject() : null;

        for (int i = 0; i < titles.Count; i++)
        {
            var entry = new OpenSearchResultEntry
            {
                Title = (string)titles[i]!,
                Description = (string?)descs?[i],
                Url = (string?)urls?[i],
            };
            result.Add(entry);
        }
        return result;
    }

}

/// <summary>
/// Options for opensearch.
/// </summary>
[Flags]
public enum OpenSearchOptions
{

    /// <summary>No options.</summary>
    None = 0,

    /// <summary>
    /// Return the target page when meeting redirects.
    /// This may cause OpenSearch return fewer results than limitation.
    /// </summary>
    ResolveRedirects = 1,

}

/// <summary>
/// Represents an entry in opensearch result.
/// </summary>
public sealed record OpenSearchResultEntry
{

    /// <summary>
    /// Title of the page.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Url of the page. May be null.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Description of the page. May be null.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Title + (Description != null ? ":" + Description : null);
    }

}
