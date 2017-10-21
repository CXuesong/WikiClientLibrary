using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures.Logging;

namespace WikiClientLibrary.Wikia.WikiaApi
{

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
            if (userName.IndexOf(',') >= 0)
                throw new ArgumentException("User name cannot contain comma (,).", nameof(userName));
            JToken jresult;
            try
            {
                jresult = await site.InvokeWikiaApiAsync("/User/Details",
                    new WikiaQueryRequestMessage(new {ids = userName}), cancellationToken);
            }
            catch (WikiaApiException ex) when (ex.ErrorType == "NotFoundApiException")
            {
                return null;
            }
            var user = jresult["items"][0].ToObject<UserInfo>();
            if (user == null) return null;
            var basePath = (string)jresult["basepath"];
            if (basePath != null) user.ApplyBasePath(basePath);
            return user;
        }

        /// <summary>
        /// Asynchronously fetches the specified users' information.
        /// </summary>
        /// <param name="site">The site to issue the request.</param>
        /// <param name="userNames">The user names to be fetched.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="userNames"/> is <c>null</c>.</exception>
        /// <returns>
        /// An asynchronous sequence containing the detailed user information.
        /// The user names are normalized by the server. Inexistent user names are skipped.
        /// </returns>
        public static IAsyncEnumerable<UserInfo> FetchUsersAsync(this WikiaSite site, IEnumerable<string> userNames)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (userNames == null) throw new ArgumentNullException(nameof(userNames));
            return AsyncEnumerableFactory.FromAsyncGenerator<UserInfo>(async (sink, ct) =>
            {
                using (site.BeginActionScope(null, userNames as ICollection))
                {
                    foreach (var names in userNames.Partition(100))
                    {
                        JToken jresult;
                        try
                        {
                            jresult = await site.InvokeWikiaApiAsync("/User/Details",
                                new WikiaQueryRequestMessage(new {ids = string.Join(", ", names)}), ct);
                        }
                        catch (WikiaApiException ex) when (ex.ErrorType == "NotFoundApiException")
                        {
                            // All the usesers in this batch are not found.
                            // Pity.
                            continue;
                        }
                        var basePath = (string)jresult["basepath"];
                        var users = jresult["items"].ToObject<ICollection<UserInfo>>();
                        if (basePath != null)
                        {
                            foreach (var user in users)
                                user.ApplyBasePath(basePath);
                        }
                        await sink.YieldAndWait(users);
                    }
                }
            });
        }

        /// <inheritdoc cref="FetchWikiVariablesAsync(WikiaSite,CancellationToken)"/>
        public static Task<SiteVariableData> FetchWikiVariablesAsync(this WikiaSite site)
        {
            return FetchWikiVariablesAsync(site, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously fetches the site information.
        /// </summary>
        /// <param name="site">The site to issue the request.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns></returns>
        public static async Task<SiteVariableData> FetchWikiVariablesAsync(this WikiaSite site, CancellationToken cancellationToken)
        {
            var jresult = await site.InvokeWikiaApiAsync("/Mercury/WikiVariables", new WikiaQueryRequestMessage(), cancellationToken);
            var jdata = jresult["data"];
            if (jdata == null) throw new UnexpectedDataException("Missing data node in the JSON response.");
            return jdata.ToObject<SiteVariableData>();
        }

        /// <inheritdoc cref="FetchRelatedPagesAsync(WikiaSite,int,int,CancellationToken)"/>
        /// <remarks>This overload fetches 10 items at most.</remarks>
        public static Task<IList<RelatedPageItem>> FetchRelatedPagesAsync(this WikiaSite site, int pageId)
        {
            return FetchRelatedPagesAsync(site, pageId, 10, CancellationToken.None);
        }

        /// <inheritdoc cref="FetchRelatedPagesAsync(WikiaSite,int,int,CancellationToken)"/>
        public static Task<IList<RelatedPageItem>> FetchRelatedPagesAsync(this WikiaSite site, int pageId, int maxCount)
        {
            return FetchRelatedPagesAsync(site, pageId, maxCount, CancellationToken.None);
        }

        private static readonly RelatedPageItem[] emptyRelatedPages = { };

        /// <summary>
        /// Asynchronously fetches the specified page's related pages.
        /// </summary>
        /// <param name="site">The site to issue the request.</param>
        /// <param name="pageId">ID of the page to find the related ones.</param>
        /// <param name="maxCount">Maximum count of the returned items.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageId"/> is less than or equals to 0.</exception>
        /// <returns></returns>
        public static async Task<IList<RelatedPageItem>> FetchRelatedPagesAsync(this WikiaSite site,
            int pageId, int maxCount, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            var jresult = await site.InvokeWikiaApiAsync("/RelatedPages/List",
                new WikiaQueryRequestMessage(new {ids = pageId, limit = maxCount}),
                cancellationToken);
            var jitems = jresult["items"][pageId.ToString()];
            if (jitems == null) jitems = ((JProperty)jresult["items"].First)?.Value;
            if (jitems == null || !jitems.HasValues) return emptyRelatedPages;
            var items = jitems.ToObject<IList<RelatedPageItem>>();
            var basePath = (string)jresult["basepath"];
            if (basePath != null)
            {
                foreach (var i in items) i.ApplyBasePath(basePath);
            }
            return items;
        }

    }
}
