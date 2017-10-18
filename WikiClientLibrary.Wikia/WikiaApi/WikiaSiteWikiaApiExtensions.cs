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
            return FetchUserAsync(site, userName, new CancellationToken());
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
                    new WikiaQueryRequestMessage(new { ids = userName }), cancellationToken);
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
                                new WikiaQueryRequestMessage(new { ids = string.Join(", ", names) }), ct);
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

    }
}
