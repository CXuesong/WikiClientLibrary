using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AsyncEnumerableExtensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{

    /// <summary>
    /// Represents a page-property-based MediaWiki generator
    /// (or sequence, see <a href="https://www.mediawiki.org/wiki/API:Generator">mw:API:Generator</a>)
    /// of <see cref="WikiPage"/>.
    /// </summary>
    /// <remarks>
    /// <para>For a common implementations for MediaWiki generator, prefer inheriting from
    /// <see cref="WikiPagePropertyGenerator{TPage}"/>, which returns <see cref="WikiPageStub"/>
    /// for <see cref="WikiList{T}.EnumItemsAsync"/> implementation.</para>
    /// <para>
    /// For <c>generator</c>s, it's not necessary for the sequence taken out
    /// from <see cref="EnumPagesAsync(PageQueryOptions)"/> to be kept ordered.
    /// If you need ordered sequence, for example,
    /// enumerating the links from the beginning of an article using <see cref="LinksGenerator"/>,
    /// use <see cref="WikiPagePropertyList{T}.EnumItemsAsync"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WikiPageGenerator{TItem,TPage}"/>
    public abstract class WikiPagePropertyGenerator<TItem, TPage> : WikiPagePropertyList<TItem>, IWikiPageGenerator<TPage>
        where TPage : WikiPage
    {
        /// <inheritdoc/>
        internal WikiPagePropertyGenerator(WikiSite site) : base(site)
        {
        }

        public virtual string GeneratorName => PropertyName;

        /// <summary>
        /// Fills generator parameters for <c>action=query&amp;generator=</c> request.
        /// </summary>
        /// <returns>A sequence of fields, which will override the basic query parameters.</returns>
        public virtual IEnumerable<KeyValuePair<string, object>> EnumGeneratorParameters()
        {
            return EnumListParameters().Select(p => new KeyValuePair<string, object>("g" + p.Key, p.Value));
        }

        /// <summary>
        /// When using the default implementation of <see cref="EnumPagesAsync(PageQueryOptions)"/>,
        /// determines whether to remove duplicate page results generated from generator results.
        /// </summary>
        /// <remarks>If the derived class has overridden <see cref="EnumPagesAsync(PageQueryOptions)"/>,
        /// the value of this property might be ignored.</remarks>
        protected virtual bool DistinctGeneratedPages => false;

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        public IAsyncEnumerable<TPage> EnumPagesAsync()
        {
            return EnumPagesAsync(PageQueryOptions.None);
        }

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        /// <remarks>
        /// For <c>generator</c>s, it's not necessary for the sequence taken out
        /// from <see cref="EnumPagesAsync(PageQueryOptions)"/> to be kept ordered.
        /// See the "remarks" part of <see cref="WikiPagePropertyGenerator{TItem,TPage}"/> for more information.
        /// </remarks>
        public virtual IAsyncEnumerable<TPage> EnumPagesAsync(PageQueryOptions options)
        {
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when using generators.", nameof(options));
            var queryParams = RequestHelper.GetPageFetchingParams(options);
            queryParams.Add("generator", GeneratorName);
            queryParams.Add("titles", PageTitle);
            foreach (var v in EnumGeneratorParameters())
                queryParams[v.Key] = v.Value;
            return RequestHelper.QueryWithContinuation(Site, queryParams,
                () => Site.BeginActionScope(this, options),
                DistinctGeneratedPages)
                .SelectMany(jquery => WikiPage.FromJsonQueryResult(Site, jquery, options).Cast<TPage>()
                    .ToAsyncEnumerable());
        }
    }


    /// <summary>
    /// The base classes for commonly-used <see cref="WikiPage"/> generator that implements
    /// <see cref="WikiPagePropertyGenerator{TItem,TPage}"/>, where <c>TItem</c> is <see cref="WikiPageStub"/>.
    /// </summary>
    /// <remarks>
    /// <para>For a more generic implementations for page-property-based MediaWiki generator,
    /// or implementations returning a sequence where the item type is not <see cref="WikiPageStub"/>,
    /// please derive your generator from <see cref="WikiPagePropertyGenerator{TItem,TPage}"/>.</para>
    /// </remarks>
    public abstract class WikiPagePropertyGenerator<TPage> : WikiPagePropertyGenerator<WikiPageStub, TPage>
        where TPage : WikiPage
    {
        /// <inheritdoc />
        protected WikiPagePropertyGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        protected override WikiPageStub ItemFromJson(JToken json, JObject jpage)
        {
            // pageid can be missing in this case.
            return new WikiPageStub((int?)json["pageid"] ?? 0, (string)json["title"], (int)json["ns"]);
        }

    }

}
