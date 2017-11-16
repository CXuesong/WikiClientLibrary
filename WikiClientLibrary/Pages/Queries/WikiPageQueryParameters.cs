using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages.Queries
{
    /// <summary>
    /// Provides basic MediaWiki API request parameters for <c>action=query</c> request.
    /// </summary>
    public interface IWikiPageQueryParameters
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
        /// Parses one or more property groups from the given<c>action=query</c> JSON response.
        /// </summary>
        /// <param name="json">One of the item node under the JSON path <c>query/pages</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
        /// <returns>A sequence of property group instance, or <see cref="Enumerable.Empty{IWikiPagePropertyGroup}"/> if no property groups available.</returns>
        IEnumerable<IWikiPagePropertyGroup> ParsePropertyGroups(JObject json);
    }

    /// <summary>
    /// Provides basic MediaWiki API request parameters for <c>action=query</c> request.
    /// </summary>
    public class WikiPageQueryParameters : IWikiPageQueryParameters
    {

        private ICollection<IWikiPagePropertyProvider<IWikiPagePropertyGroup>> _Properties;

        /// <summary>
        /// Resolves directs automatically. This may later change <see cref="WikiPage.Title"/>.
        /// This option cannot be used with generators.
        /// In the case of multiple redirects, all redirects will be resolved.
        /// </summary>
        public bool ResolveRedirects { get; set; }

        /// <summary>
        /// Gets/sets the page properties to fetch from MediaWiki site.
        /// </summary>
        public ICollection<IWikiPagePropertyProvider<IWikiPagePropertyGroup>> Properties
        {
            get
            {
                if (_Properties == null) _Properties = new List<IWikiPagePropertyProvider<IWikiPagePropertyGroup>>();
                return _Properties;
            }
            set { _Properties = value; }
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            var propBuilder = new StringBuilder("info|categoryinfo|imageinfo|pageprops");
            var p = new KeyValuePairs<string, object>
            {
                {"action", "query"},
                {"inprop", "protection"},
                {"iiprop", "timestamp|user|comment|url|size|sha1"},
                {"redirects", ResolveRedirects},
                {"maxlag", 5},
            };
            if (_Properties != null)
            {
                foreach (var prop in _Properties)
                {
                    if (prop.PropertyName != null)
                    {
                        propBuilder.Append('|');
                        propBuilder.Append(prop.PropertyName);
                    }
                    p.AddRange(prop.EnumParameters());
                }
            }
            p.Add("prop", propBuilder.ToString());
            return p;
        }

        /// <inheritdoc />
        public int GetMaxPaginationSize(bool apiHighLimits)
        {
            int limit;
            limit = apiHighLimits ? 500 : 5000;
            if (_Properties != null)
            {
                foreach (var prop in _Properties)
                {
                    limit = Math.Min(limit, prop.GetMaxPaginationSize(apiHighLimits));
                }
            }
            return limit;
        }

        /// <inheritdoc />
        public IEnumerable<IWikiPagePropertyGroup> ParsePropertyGroups(JObject json)
        {
            foreach (var provider in _Properties)
            {
                var group = provider.ParsePropertyGroup(json);
                if (group != null) yield return group;
            }
        }
    }
}
