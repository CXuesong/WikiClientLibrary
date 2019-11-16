using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Generators.Primitive
{

    /// <summary>
    /// Provides more options on detailed behavior of <see cref="WikiList{T}"/>-derived instances.
    /// Most of the options are provided for compatibility purpose.
    /// </summary>
    public class WikiListCompatibilityOptions
    {

        /// <summary>
        /// Specifies the behavior when <see cref="WikiList{T}"/> detects itself is getting into a loop
        /// due to the continuation parameter set provided by the server shares the exact same value.
        /// </summary>
        public WikiListContinuationLoopBehaviors ContinuationLoopBehaviors { get; set; }

    }

    /// <summary>
    /// Controls the behavior when <see cref="WikiList{T}"/> detects itself is getting into a loop
    /// due to the continuation parameter set provided by the server has the exact same values
    /// as query parameters.
    /// </summary>
    /// <remarks>
    /// On old MediaWiki builds with <a href="https://www.mediawiki.org/wiki/API:Raw_query_continue">raw query continuation</a>,
    /// if there are too many logs in the same timestamp (seconds precision), such situation can happen. If there are 100 logs sharing
    /// the same timestamp (truncated into seconds), while we only take first 50 of them as the first page,
    /// the continuation parameter set will indicate the next batch starts with the same timestamp as the first item,
    /// eventually causing client to fetch the next batch with the exactly same set of parameters.
    /// </remarks>
    [Flags]
    public enum WikiListContinuationLoopBehaviors
    {
        /// <summary>
        /// Do nothing. This will cause an <see cref="UnexpectedDataException"/> to be thrown.
        /// </summary>
        None = 0,
        /// <summary>
        /// Tries to fetch more items, so the last item might have a different timestamp, causing the continuation continues.
        /// <see cref="WikiList{T}"/> will fetch for 1000 items at most, depending on whether the user has `apihighlimits` right.
        /// If it still cannot get out of the continuation loop, an <see cref="UnexpectedDataException"/> will be thrown.
        /// </summary>
        FetchMore = 1,

        // TODO implement the following options.
        ///// <summary>
        ///// Tries to increment/decrement the raw query continuation parameter value, so as to get out of the continuation loop,
        ///// at the cost of skipping some of the items.
        ///// </summary>
        //SkipItems = 2,
        ///// <summary>
        ///// Tries <see cref="FetchMore"/> first, then <see cref="SkipItems"/>.
        ///// </summary>
        //FetchMoreThenSkip = FetchMore | SkipItems,
    }

}
