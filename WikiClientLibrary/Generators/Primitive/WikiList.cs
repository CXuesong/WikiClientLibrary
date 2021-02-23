using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{

    /// <summary>
    /// Provides method for asynchronously generating a sequence of items.
    /// </summary>
    /// <typeparam name="T">The page instance type.</typeparam>
    public interface IWikiList<out T>
    {

        /// <summary>
        /// Asynchronously enumerates all the items in the list.
        /// </summary>
        /// <remarks>In most cases, the whole sequence will be very long. To take only the top <c>n</c> results
        /// from the sequence, chain the returned <see cref="IAsyncEnumerable{T}"/> with <see cref="AsyncEnumerable.Take{TSource}"/>
        /// extension method.</remarks>
        IAsyncEnumerable<T> EnumItemsAsync(CancellationToken cancellationToken = default);

    }

    /// <summary>
    /// Represents a configured MediaWiki <c>list</c>. (<a href="https://www.mediawiki.org/wiki/API:Lists">mw:API:Lists</a>)
    /// </summary>
    /// <typeparam name="T">The type of listed items.</typeparam>
    /// <seealso cref="WikiPageGenerator{TItem}"/>
    /// <seealso cref="WikiPagePropertyList{T}"/>
    public abstract class WikiList<T> : IWikiList<T>
    {

        private int _PaginationSize = 10;

        /// <param name="site">The MediaWiki site this instance applies to.</param>
        public WikiList(WikiSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
        }

        /// <summary>
        /// Gets/sets the compatibility options used with this list.
        /// </summary>
        public WikiListCompatibilityOptions? CompatibilityOptions { get; set; }

        /// <summary>Gets the MediaWiki site this instance applies to.</summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Gets/sets maximum items returned per MediaWiki API invocation.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per MediaWiki API invocation.
        /// This limit is 10 by default, and can be set as high as 500 for regular users,
        /// or 5000 for users with the <c>apihighlimits</c> right (typically in bot or sysop group).
        /// </value>
        /// <remarks>
        /// This property decides how many items returned at most per MediaWiki API invocation.
        /// Note that the enumerator returned from <see cref="EnumItemsAsync"/>
        /// or <seealso cref="WikiPageGenerator{TItem}.EnumPagesAsync()"/>
        /// will automatically make further MediaWiki API invocations to ask for the next batch of results,
        /// when needed.
        /// </remarks>
        public int PaginationSize
        {
            get { return _PaginationSize; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _PaginationSize = value;
            }
        }

        /// <summary>
        /// The name of list, used as the value of <c>list</c> parameter in <c>action=query</c> request.
        /// </summary>
        public abstract string ListName { get; }

        /// <summary>
        /// When overridden, fills generator parameters for <c>action=query&amp;list={ListName}</c> request.
        /// </summary>
        /// <returns>A sequence of fields, which will override the basic query parameters.</returns>
        public abstract IEnumerable<KeyValuePair<string, object?>> EnumListParameters();

        /// <summary>
        /// Parses an item contained in the <c>action=query&amp;list=</c> JSON response.
        /// </summary>
        /// <param name="json">One of the item node under the JSON path <c>query/{listname}</c>.</param>
        /// <returns>The item that will be returned in the sequence from <see cref="EnumItemsAsync"/>.</returns>
        protected abstract T ItemFromJson(JToken json);

        /// <summary>
        /// When overriden, called when there is exception raised when
        /// executing the asynchronous generator function inside <see cref="EnumItemsAsync"/>.
        /// </summary>
        /// <param name="exception">The raised exception.</param>
        /// <remarks>
        /// <para>
        /// Implementation can throw other more specific errors in the implementation.
        /// The caller will throw the original exception after calling this method
        /// if this function does not throw any other exception in the implementation.
        /// </para>
        /// <para>
        /// The default implementation does nothing.
        /// </para>
        /// </remarks>
        protected virtual void OnEnumItemsFailed(Exception exception)
        {
        }

        /// <inheritdoc />
        /// <exception cref="OperationFailedException">
        /// (When enumerating) There is any MediaWiki API failure during the operation.
        /// </exception>
        /// <exception cref="Exception">
        /// (When enumerating) There can be other types of errors thrown.
        /// See the respective <see cref="OnEnumItemsFailed"/> override documentations in the implementation classes.
        /// </exception>
        public async IAsyncEnumerable<T> EnumItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var baseQueryParams = new Dictionary<string, object?> { { "action", "query" }, { "maxlag", 5 }, { "list", ListName }, };
            foreach (var p in EnumListParameters())
                baseQueryParams.Add(p.Key, p.Value);
            cancellationToken.ThrowIfCancellationRequested();
            var continuationParams = new Dictionary<string, object?>();
            using var scope = Site.BeginActionScope(this);
            // query parameters for this batch. The content/ref will be modified below.
            var queryParams = new Dictionary<string, object?>();
            while (true)
            {
                queryParams.Clear();
                queryParams.MergeFrom(baseQueryParams);
                queryParams.MergeFrom(continuationParams);
                JToken jresult;
                JToken? listNode;
                try
                {
                    jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams), cancellationToken);
                    listNode = RequestHelper.FindQueryResponseItemsRoot(jresult, ListName);
                }
                catch (Exception ex)
                {
                    OnEnumItemsFailed(ex);
                    throw;
                }
                if (listNode != null)
                {
                    using (ExecutionContextStash.Capture())
                    {
                        foreach (var n in listNode)
                            yield return ItemFromJson(n);
                    }
                }
                // Check for continuation.
                switch (RequestHelper.ParseContinuationParameters(jresult, queryParams, continuationParams))
                {
                    case RequestHelper.CONTINUATION_DONE:
                        yield break;
                    case RequestHelper.CONTINUATION_AVAILABLE:
                        if (listNode == null)
                            Site.Logger.LogWarning("Empty query page with continuation received.");
                        break;
                    case RequestHelper.CONTINUATION_LOOP:
                        Site.Logger.LogWarning("Continuation information provided by server response leads to infinite loop. {RawData}",
                            RequestHelper.FindQueryContinuationParameterRoot(jresult));
                        // The following is just last effort.
                        var outOfLoop = false;
                        if (CompatibilityOptions != null)
                        {
                            if ((CompatibilityOptions.ContinuationLoopBehaviors & WikiListContinuationLoopBehaviors.FetchMore) ==
                                WikiListContinuationLoopBehaviors.FetchMore)
                            {
                                // xxlimit (length = 7)
                                var limitParamName =
                                    queryParams.Keys.FirstOrDefault(k => k.Length == 7 && k.EndsWith("limit", StringComparison.Ordinal));
                                if (limitParamName == null)
                                {
                                    Site.Logger.LogWarning("Failed to find the underlying parameter name for PaginationSize.");
                                }
                                else
                                {
                                    var maxLimit = Site.AccountInfo.HasRight(UserRights.ApiHighLimits) ? 1000 : 500;
                                    var currentLimit = Math.Max(PaginationSize, 50);
                                    // Continuously expand PaginationSize, hopefully we can retrieve some different continuation param value.
                                    while (currentLimit < maxLimit)
                                    {
                                        currentLimit = Math.Min(maxLimit, currentLimit * 2);
                                        Site.Logger.LogDebug("Try to fetch more with {ParamName}={ParamValue}.", limitParamName, currentLimit);
                                        queryParams.Clear();
                                        queryParams.MergeFrom(baseQueryParams);
                                        queryParams.MergeFrom(continuationParams);
                                        queryParams[limitParamName] = currentLimit;
                                        var jresult2 = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams),
                                            cancellationToken);
                                        var applyResult = RequestHelper.ParseContinuationParameters(jresult2, queryParams, continuationParams);
                                        switch (applyResult)
                                        {
                                            case RequestHelper.CONTINUATION_AVAILABLE:
                                            case RequestHelper.CONTINUATION_DONE:
                                                var listNode2 = RequestHelper.FindQueryResponseItemsRoot(jresult2, ListName);
                                                Site.Logger.LogInformation("Successfully got out of the continuation loop.");
                                                if (listNode2 != null)
                                                {
                                                    if (listNode != null)
                                                    {
                                                        // Eliminate items that we have already yielded.
                                                        var yieldedItems = new HashSet<JToken>(listNode, new JTokenEqualityComparer());
                                                        using (ExecutionContextStash.Capture())
                                                            foreach (var n in listNode2.Where(n => !yieldedItems.Contains(n)))
                                                                yield return ItemFromJson(n);
                                                    }
                                                    else
                                                    {
                                                        using (ExecutionContextStash.Capture())
                                                            foreach (var n in listNode2)
                                                                yield return ItemFromJson(n);
                                                    }
                                                }
                                                outOfLoop = true;
                                                if (applyResult == RequestHelper.CONTINUATION_DONE)
                                                    yield break;
                                                break;
                                            case RequestHelper.CONTINUATION_LOOP:
                                                break;
                                        }
                                    }

                                }
                            }
                            //if (!outOfLoop && (CompatibilityOptions.ContinuationLoopBehaviors & WikiListContinuationLoopBehaviors.SkipItems) ==
                            //    WikiListContinuationLoopBehaviors.SkipItems)
                            //{

                            //}
                        }
                        if (!outOfLoop)
                            throw new UnexpectedDataException(Prompts.ExceptionUnexpectedContinuationLoop);
                        break;
                }
            }
        }

    }

}
