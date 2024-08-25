using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Wikia.Sites;

namespace WikiClientLibrary.Wikia.WikiaApi;

/// <summary>Extension methods that implements Wikia REST-ful API.</summary>
public static class WikiaSiteWikiaApiExtensions
{

    /// <inheritdoc cref="FetchUserAsync(WikiaSite,string,CancellationToken)"/>
    public static Task<UserInfo> FetchUserAsync(this WikiaSite site, string userName)
    {
        return FetchUserAsync(site, userName, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously fetches the specified user's information.
    /// </summary>
    /// <param name="site">The site to issue the request.</param>
    /// <param name="userName">The user name to be fetched.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="userName"/> is <c>null</c>.</exception>
    /// <returns>A task that returns the requested user information, or <c>null</c> for inexistent user.</returns>
    public static async Task<UserInfo> FetchUserAsync(this WikiaSite site,
        string userName, CancellationToken cancellationToken)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (userName == null) throw new ArgumentNullException(nameof(userName));
        if (userName.Contains(','))
            throw new ArgumentException("User name cannot contain comma (,).", nameof(userName));
        using (site.BeginActionScope(null, (object)userName))
        {
            JsonNode jresult;
            try
            {
                jresult = await site.InvokeWikiaApiAsync("/User/Details",
                    new WikiaQueryRequestMessage(new { ids = NormalizeUserName(userName) }), cancellationToken);
            }
            catch (NotFoundApiException)
            {
                return null;
            }
            var user = jresult["items"][0].Deserialize<UserInfo>();
            if (user == null) return null;
            var basePath = (string)jresult["basepath"];
            if (basePath != null) user.ApplyBasePath(basePath);
            return user;
        }
    }

    /// <summary>
    /// Asynchronously fetches the specified users' information.
    /// </summary>
    /// <param name="site">The site to issue the request.</param>
    /// <param name="userNames">The usernames to be fetched.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="userNames"/> is <c>null</c>.</exception>
    /// <returns>
    /// An asynchronous sequence containing the detailed user information.
    /// The usernames are normalized locally to ensure the first character is upper-case. Non-existent usernames are skipped.
    /// </returns>
    public static async IAsyncEnumerable<UserInfo> FetchUsersAsync(this WikiaSite site, IEnumerable<string> userNames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (userNames == null) throw new ArgumentNullException(nameof(userNames));
        using (site.BeginActionScope(null, (object)(userNames as ICollection)))
        {
            foreach (var names in userNames.Partition(100))
            {
                JsonNode jresult;
                // 2024-08: Wikia API won't normalize the names for us anymore.
                // So we will have to this by ourselves.
                var normalizedNames = names.Select(NormalizeUserName).ToList();
                try
                {
                    jresult = await site.InvokeWikiaApiAsync("/User/Details",
                        // 2024-08: There cannot be any whitespace around ","
                        new WikiaQueryRequestMessage(new { ids = string.Join(',', normalizedNames) }), cancellationToken);
                }
                catch (WikiaApiException ex) when (ex.ErrorType == "NotFoundApiException")
                {
                    // All the users in this batch are not found.
                    // Pity.
                    continue;
                }
                var basePath = (string)jresult["basepath"];
                var userItemDict = jresult["items"].AsArray().Select(i => i.Deserialize<UserInfo>()).ToDictionary(i => i.Name);
                if (basePath != null)
                {
                    foreach (var user in userItemDict.Values)
                        user.ApplyBasePath(basePath);
                }
                using (ExecutionContextStash.Capture())
                    foreach (var name in normalizedNames)
                    {
                        if (userItemDict.TryGetValue(name, out var user))
                            yield return user;
                    }
            }
        }
    }

    private static string NormalizeUserName(string userName)
    {
        // TODO Possibly reuse Utility.NormalizeTitlePart
        userName = userName.Trim(' ', '\t', '_');
        if (userName.Length > 0)
        {
            var char0 = char.ToUpperInvariant(userName[0]);
            if (userName[0] != char0) userName = char0 + userName[1..];
        }
        return userName;
    }

    /// <inheritdoc cref="FetchRelatedPagesAsync(WikiaSite,long,int,CancellationToken)"/>
    /// <remarks>This overload fetches 10 items at most.</remarks>
    public static Task<IList<RelatedPageItem>> FetchRelatedPagesAsync(this WikiaSite site, long pageId)
    {
        return FetchRelatedPagesAsync(site, pageId, 10, CancellationToken.None);
    }

    /// <inheritdoc cref="FetchRelatedPagesAsync(WikiaSite,long,int,CancellationToken)"/>
    public static Task<IList<RelatedPageItem>> FetchRelatedPagesAsync(this WikiaSite site, long pageId, int maxCount)
    {
        return FetchRelatedPagesAsync(site, pageId, maxCount, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously fetches the specified page's related pages.
    /// </summary>
    /// <param name="site">The site to issue the request.</param>
    /// <param name="pageId">ID of the page to find the related ones.</param>
    /// <param name="maxCount">Maximum count of the returned items.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="site"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageId"/> is less than or equals to 0.</exception>
    /// <exception cref="NotFoundApiException"><c>Related Pages</c> extension is not available.</exception>
    /// <returns></returns>
    public static async Task<IList<RelatedPageItem>> FetchRelatedPagesAsync(this WikiaSite site,
        long pageId, int maxCount, CancellationToken cancellationToken)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
        using (site.BeginActionScope(null, pageId, maxCount))
        {
            var jresult = await site.InvokeWikiaApiAsync("/RelatedPages/List",
                new WikiaQueryRequestMessage(new { ids = pageId, limit = maxCount }),
                cancellationToken);
            var jitems = jresult["items"][pageId.ToString(CultureInfo.InvariantCulture)]?.AsArray();
            if (jitems == null) jitems = jresult["items"]?.AsObject().FirstOrDefault().Value?.AsArray();
            if (jitems == null || jitems.Count == 0) return Array.Empty<RelatedPageItem>();
            var items = jitems.Deserialize<List<RelatedPageItem>>();
            var basePath = (string)jresult["basepath"];
            if (basePath != null)
            {
                foreach (var i in items) i.ApplyBasePath(basePath);
            }
            return items;
        }
    }

}
