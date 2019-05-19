using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{
    /// <summary>
    /// Provides method for asynchronously generating a sequence of <see cref="WikiPage"/>
    /// with information fetched from server.
    /// </summary>
    public interface IWikiPageGenerator
    {
        /// <summary>
        /// Asynchronously generates the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        /// <remarks>In most cases, the whole sequence will be very long. To take only the top <c>n</c> results
        /// from the sequence, chain the returned <see cref="IAsyncEnumerable{T}"/> with <see cref="AsyncEnumerable.Take{TSource}"/>
        /// extension method.</remarks>
        IAsyncEnumerable<WikiPage> EnumPagesAsync(IWikiPageQueryProvider options);

    }

    /// <summary>
    /// Represents a <c>list</c>-based MediaWiki generator
    /// (or sequence, see <a href="https://www.mediawiki.org/wiki/API:Generator">mw:API:Generator</a>)
    /// of <see cref="WikiPage"/>.
    /// </summary>
    /// <typeparam name="TItem">The type of listed items.</typeparam>
    /// <remarks>
    /// <para>For common implementations for MediaWiki generator, prefer inheriting from
    /// <see cref="WikiPageGenerator"/>, which returns <see cref="WikiPageStub"/>
    /// for <see cref="WikiList{T}.EnumItemsAsync"/> implementation.</para>
    /// </remarks>
    /// <seealso cref="WikiPagePropertyGenerator{TItem}"/>
    public abstract class WikiPageGenerator<TItem> : WikiList<TItem>, IWikiPageGenerator
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
        /// When using the default implementation of <see cref="EnumPagesAsync(IWikiPageQueryProvider)"/>,
        /// determines whether to remove duplicate page results generated from generator results.
        /// </summary>
        /// <remarks>If the derived class has overridden <see cref="EnumPagesAsync(IWikiPageQueryProvider)"/>,
        /// the value of this property might be ignored.</remarks>
        protected virtual bool DistinctGeneratedPages => false;

        ///// <summary>
        ///// Parses an item contained in the <c>action=query&amp;list=</c> JSON response.
        ///// </summary>
        ///// <param name="json">One of the item node under the JSON path <c>query/{listname}</c>.</param>
        ///// <returns>The item that will be returned in the sequence from <see cref="EnumPagesAsync()"/>.</returns>
        //protected virtual WikiPage PageFromJson(JObject json, IWikiPageQueryProvider options)
        //{
        //    var page = new WikiPage(Site, 0);
        //    MediaWikiHelper.PopulatePageFromJson(page, json, options);
        //    return page;
        //}

        /// <summary>
        /// Asynchronously generates the sequence of pages.
        /// </summary>
        public IAsyncEnumerable<WikiPage> EnumPagesAsync()
        {
            return EnumPagesAsync(MediaWikiHelper.QueryProviderFromOptions(PageQueryOptions.None));
        }

        /// <summary>
        /// Asynchronously generates the sequence of pages.
        /// </summary>
        public IAsyncEnumerable<WikiPage> EnumPagesAsync(PageQueryOptions options)
        {
            return EnumPagesAsync(MediaWikiHelper.QueryProviderFromOptions(options));
        }

        /// <summary>
        /// Asynchronously generates the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        public virtual IAsyncEnumerable<WikiPage> EnumPagesAsync(IWikiPageQueryProvider options)
        {
            var queryParams = options.EnumParameters(Site.SiteInfo.Version).ToDictionary();
            queryParams.Add("generator", GeneratorName);
            foreach (var v in EnumGeneratorParameters())
                queryParams[v.Key] = v.Value;
            return RequestHelper.QueryWithContinuation(Site, queryParams,
                    () => Site.BeginActionScope(this, options),
                    DistinctGeneratedPages)
                .SelectMany(jquery => WikiPage.FromJsonQueryResult(Site, jquery, options).ToAsyncEnumerable());
        }
    }

    /// <summary>
    /// The base classes for commonly-used <see cref="WikiPage"/> generator that implements
    /// <see cref="WikiPageGenerator{TItem}"/>, where <c>TItem</c> is <see cref="WikiPageStub"/>.
    /// </summary>
    /// <remarks>
    /// <para>For a more generic implementations for MediaWiki generator, or implementations returning
    /// sequence where the item type is not <see cref="WikiPageStub"/>,
    /// please derive your generator from <see cref="WikiPageGenerator{TItem}"/>.</para>
    /// </remarks>
    public abstract class WikiPageGenerator : WikiPageGenerator<WikiPageStub>
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
