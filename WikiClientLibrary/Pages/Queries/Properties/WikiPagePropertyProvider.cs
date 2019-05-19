using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    /// <summary>
    /// Used in client-side to implement different <a href="https://www.mediawiki.org/wiki/API:Query">query</a> modules
    /// by providing extra parameters for <c>action=query</c> requests.
    /// </summary>
    /// <typeparam name="T">The type of property group that will be attached to every processed <see cref="WikiPage"/>.</typeparam>
    /// <remarks>You can use <see cref="WikiPage.GetPropertyGroup{T}"/> to retrieve the property groups attached to a wiki page.</remarks>
    /// <seealso cref="WikiPage.RefreshAsync(IWikiPageQueryProvider)"/>
    public interface IWikiPagePropertyProvider<out T> where T : IWikiPagePropertyGroup
    {

        /// <summary>
        /// Enumerates the MediaWiki API request parameters for <c>action=query</c> request.
        /// </summary>
        /// <param name="version">MediaWiki API version. Use <seealso cref="MediaWikiVersion.Zero"/> for unknown version or compatible mode.</param>
        IEnumerable<KeyValuePair<string, object>> EnumParameters(MediaWikiVersion version);

        /// <summary>
        /// Gets the maximum allowed count of titles in each MediaWiki API request.
        /// </summary>
        /// <param name="version">MediaWiki API version. Use <seealso cref="MediaWikiVersion.Zero"/> for unknown version or compatible mode.</param>
        /// <param name="apiHighLimits">Whether the account has <c>api-highlimits</c> right.</param>
        /// <returns>
        /// The maximum allowed count of titles in each MediaWiki API request.
        /// This applies to the values of <c>ids=</c> and <c>titles=</c> parameters
        /// for <c>action=query</c> request.
        /// </returns>
        int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits);

        /// <summary>
        /// Gets the property name, when this property is needed to be included in the <c>prop=</c> parameter
        /// in the MediaWiki API request.
        /// </summary>
        /// <value>A property name used as a part of <c>prop=</c> parameter, or <c>null</c> if this
        /// property provider does not require extra value added to <c>prop=</c> parameter.</value>
        string PropertyName { get; }

        /// <summary>
        /// Parses the properties from the given<c>action=query</c> JSON response.
        /// </summary>
        /// <param name="json">One of the item node under the JSON path <c>query/pages</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
        /// <returns>A property group instance, or <c>null</c> if no extra property group available.</returns>
        T ParsePropertyGroup(JObject json);

    }

    /// <inheritdoc />
    /// <summary>
    /// Provides default implementation for <see cref="T:WikiClientLibrary.Pages.Queries.Properties.IWikiPagePropertyProvider`1" />.
    /// </summary>
    /// <typeparam name="T">The type of property group that will be attached to every processed <see cref="T:WikiClientLibrary.Pages.WikiPage" />.</typeparam>
    public abstract class WikiPagePropertyProvider<T> : IWikiPagePropertyProvider<T> where T : IWikiPagePropertyGroup
    {


        /// <inheritdoc />
        public abstract IEnumerable<KeyValuePair<string, object>> EnumParameters(MediaWikiVersion version);

        /// <inheritdoc />
        public virtual int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits)
        {
            return apiHighLimits ? 500 : 5000;
        }

        /// <inheritdoc />
        public abstract T ParsePropertyGroup(JObject json);

        /// <inheritdoc />
        public abstract string PropertyName { get; }


    }
}