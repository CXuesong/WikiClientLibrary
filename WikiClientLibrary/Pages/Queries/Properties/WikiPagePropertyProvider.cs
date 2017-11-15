using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    public interface IWikiPagePropertyProvider
    {

        /// <summary>
        /// Enumerates the MediaWiki API request parameters for <c>action=query</c> request.
        /// </summary>
        IEnumerable<KeyValuePair<string, object>> EnumParameters();

        /// <summary>
        /// Gets the maximum allowed count of titles in each MediaWiki API request.
        /// </summary>
        /// <param name="apiHighLimits">Whether the account has <c>api-highlimits</c> right.</param>
        /// <returns>
        /// The maximum allowed count of titles in each MediaWiki API request.
        /// This applies to the values of <c>ids=</c> and <c>titles=</c> parameters
        /// for <c>action=query</c> request.
        /// </returns>
        int GetMaxPaginationSize(bool apiHighLimits);

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
        /// <remarks>
        /// <para>When overriding this method, you do not need to invoke the base implementation.</para>
        /// </remarks>
        IWikiPagePropertyGroup ParsePropertyGroup(JObject json);
    }
    
    public abstract class WikiPagePropertyProvider : IWikiPagePropertyProvider
    {
        /// <inheritdoc />
        public abstract IEnumerable<KeyValuePair<string, object>> EnumParameters();

        /// <inheritdoc />
        public virtual int GetMaxPaginationSize(bool apiHighLimits)
        {
            return apiHighLimits ? 500 : 5000;
        }

        /// <inheritdoc />
        public abstract string PropertyName { get; }

        /// <inheritdoc />
        public virtual IWikiPagePropertyGroup ParsePropertyGroup(JObject json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return null;
        }
    }
}