using System;
using System.Collections.Generic;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages.Queries
{

    public interface IWikiPagePropertyQueryParameters : IWikiPageQueryParameters
    {

        /// <summary>
        /// Gets the property name, when this property is needed to be included in the <c>prop=</c> parameter
        /// in the MediaWiki API request.
        /// </summary>
        string PropertyName { get; }

    }
    
    public abstract class WikiPagePropertyQueryParameters : IWikiPagePropertyQueryParameters
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
    }
}