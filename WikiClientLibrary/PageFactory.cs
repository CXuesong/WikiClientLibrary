using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Flow;

namespace WikiClientLibrary
{
    // Factory methods
    partial class Page
    {
        /// <summary>
        /// Create an instance of <see cref="Page"/> or its derived class,
        /// depending on the namespace the page is in.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="title">Title of the page.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="title"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="title"/> has invalid title patterns.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="title"/> is an interwiki link.</exception>
        public static Page FromTitle(Site site, string title)
        {
            return FromTitle(site, title, 0);
        }

        /// <summary>
        /// Create an instance of <see cref="Page"/> or its derived class,
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
        public static Page FromTitle(Site site, string title, int defaultNamespaceId)
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
            if (link.Interwiki != null)
                throw new InvalidOperationException($"Interwiki title is not supported: {title} .");
            switch (link.Namespace.Id)
            {
                case BuiltInNamespaces.Category:
                    return new Category(site, title);
                case BuiltInNamespaces.File:
                    return new Category(site, title);
                default:
                    return new Page(site, title, defaultNamespaceId);
            }
        }

        /// <summary>
        /// Creates a list of <see cref="Page"/> based on JSON query result.
        /// </summary>
        /// <param name="site">A <see cref="Site"/> object.</param>
        /// <param name="queryNode">The <c>qurey</c> node value object of JSON result.</param>
        /// <param name="options">Provides options when performing the query.</param>
        /// <returns>Retrived pages.</returns>
        internal static IList<Page> FromJsonQueryResult(Site site, JObject queryNode, PageQueryOptions options)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (queryNode == null) throw new ArgumentNullException(nameof(queryNode));
            var pages = (JObject)queryNode["pages"];
            if (pages == null) return EmptyPages;
            return pages.Properties().Select(page =>
            {
                Page newInst;
                if (page.Value["categoryinfo"] != null)
                    newInst = new Category(site);
                else if ((string) page.Value["contentmodel"] == ContentModels.FlowBoard)
                {
                    if ((int) page["ns"] == FlowNamespaces.Topic)
                        newInst = new Topic(site);
                    else
                        newInst = new Board(site);
                }
                else
                    newInst = new Page(site);
                newInst.LoadFromJson(page, options);
                return newInst;
            }).ToList();
        }

    }
}
