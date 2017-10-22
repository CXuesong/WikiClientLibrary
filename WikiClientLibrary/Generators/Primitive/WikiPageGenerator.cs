using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{


    public interface IWikiPageGenerator<out T> where T : WikiPage
    {

        /// <summary>
        /// Asynchornously generates the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        /// <remarks>In most cases, the whole sequence will be very long. To take only the top <c>n</c> results
        /// from the sequence, chain the returned <see cref="IAsyncEnumerable{T}"/> with <see cref="AsyncEnumerable.Take{TSource}"/>
        /// extension method.</remarks>
        IAsyncEnumerable<T> EnumPagesAsync(PageQueryOptions options);

    }

    /// <summary>
    /// Represents a <c>list</c>-based MediaWiki generator
    /// (or sequence, see <a href="https://www.mediawiki.org/wiki/API:Generator">mw:API:Generator</a>)
    /// of <see cref="WikiPage"/>.
    /// </summary>
    /// <typeparam name="TItem">The type of listed items.</typeparam>
    /// <typeparam name="TPage">The type of listed <see cref="WikiPage"/>.</typeparam>
    /// <remarks>
    /// <para>For common implementations for MediaWiki generator, prefer inheriting from
    /// <see cref="WikiPageGenerator{TPage}"/>, which returns <see cref="WikiPageStub"/>
    /// for <see cref="WikiList{T}.EnumItemsAsync"/> implementation.</para>
    /// </remarks>
    public abstract class WikiPageGenerator<TItem, TPage> : WikiList<TItem>, IWikiPageGenerator<TPage>
        where TPage : WikiPage
    {

        /// <inheritdoc/>
        public WikiPageGenerator(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// The name of generator, used as the value of <c>generator</c> parameter in <c>action=query</c> request.
        /// </summary>
        public virtual string GeneratorName => ListName;

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
        public virtual IAsyncEnumerable<TPage> EnumPagesAsync(PageQueryOptions options)
        {
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when using generators.", nameof(options));
            var queryParams = RequestHelper.GetPageFetchingParams(options);
            queryParams.Add("generator", GeneratorName);
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
    /// <see cref="WikiPageGenerator{TItem,TPage}"/>, where <c>TItem</c> is <see cref="WikiPageStub"/>.
    /// </summary>
    /// <remarks>
    /// <para>For a more generic implementations for MediaWiki generator, or implementations returning
    /// sequence where the item type is not <see cref="WikiPageStub"/>,
    /// please derive your generator from <see cref="WikiPageGenerator{TItem,TPage}"/>.</para>
    /// </remarks>
    public abstract class WikiPageGenerator<TPage> : WikiPageGenerator<WikiPageStub, TPage>
        where TPage : WikiPage
    {
        /// <inheritdoc />
        protected WikiPageGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        protected override WikiPageStub ItemFromJson(JToken json)
        {
            return new WikiPageStub((int)json["pageid"], (string)json["title"], (int)json["ns"]);
        }

    }

}
