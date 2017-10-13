using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    // Factory methods
    partial class WikiPage
    {
        /// <summary>
        /// Create an instance of <see cref="WikiPage"/> or its derived class,
        /// depending on the namespace the page is in.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="title">Title of the page.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="title"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="title"/> has invalid title patterns.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="title"/> is an interwiki link.</exception>
        public static WikiPage FromTitle(WikiSite site, string title)
        {
            return FromTitle(site, title, 0);
        }

        /// <summary>
        /// Create an instance of <see cref="WikiPage"/> or its derived class,
        /// depending on the namespace the page is in.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="title">Title of the page, with or without namespace prefix.</param>
        /// <param name="defaultNamespaceId">
        /// The namespace id of the page used when there's no explicit namespace prefix in <paramref name="title"/>.
        /// See <see cref="BuiltInNamespaces"/> for a list of possible values.
        /// </param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="title"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="title"/> has invalid title patterns.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="title"/> is an interwiki link.</exception>
        public static WikiPage FromTitle(WikiSite site, string title, int defaultNamespaceId)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (title == null) throw new ArgumentNullException(nameof(title));
            WikiLink link;
            try
            {
                link = WikiLink.Parse(site, title, defaultNamespaceId);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message, nameof(title), ex);
            }
            if (link.InterwikiPrefix != null)
                throw new InvalidOperationException($"Interwiki title is not supported: {title} .");
            switch (link.Namespace.Id)
            {
                case BuiltInNamespaces.Category:
                    return new CategoryPage(site, title);
                case BuiltInNamespaces.File:
                    return new FilePage(site, title);
                default:
                    return new WikiPage(site, title, defaultNamespaceId);
            }
        }

        /// <summary>
        /// Creates a list of <see cref="WikiPage"/> based on JSON query result.
        /// </summary>
        /// <param name="site">A <see cref="Site"/> object.</param>
        /// <param name="queryNode">The <c>qurey</c> node value object of JSON result.</param>
        /// <param name="options">Provides options when performing the query.</param>
        /// <returns>Retrived pages.</returns>
        internal static IList<WikiPage> FromJsonQueryResult(WikiSite site, JObject queryNode, PageQueryOptions options)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (queryNode == null) throw new ArgumentNullException(nameof(queryNode));
            var pages = (JObject)queryNode["pages"];
            if (pages == null) return EmptyPages;
            // If query.xxx.index exists, sort the pages by the given index.
            // This is specifically used with SearchGenerator, to keep the search result in order.
            // For other generators, this property simply does not exist.
            // See https://www.mediawiki.org/wiki/API_talk:Query#On_the_order_of_titles_taken_out_of_generator .
            return pages.Properties().OrderBy(page => (int?)page.Value["index"])
                .Select(page =>
                {
                    WikiPage newInst;
                    if (page.Value["imageinfo"] != null)
                        newInst = new FilePage(site);
                    else if (page.Value["categoryinfo"] != null)
                        newInst = new CategoryPage(site);
                    else
                        newInst = new WikiPage(site);
                    newInst.LoadFromJson(page, options);
                    return newInst;
                }).ToList();
        }

    }
}
