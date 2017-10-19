using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{

    public interface IWikiPageGenerator<out T> where T : WikiPage
    {

        /// <summary>
        /// Asynchornously generate the sequence of pages.
        /// </summary>
        /// <param name="options">Options when querying for the pages.</param>
        IAsyncEnumerable<T> EnumPagesAsync(PageQueryOptions options);

    }

    /// <summary>
    /// Represents a generator (or iterator) of <see cref="WikiPage"/>.
    /// Generator implementations should use its generic version, <see cref="WikiPageGenerator{T}"/>, as base class.
    /// </summary>
    public abstract class WikiPageGenerator<TItem, TPage> : WikiList<TItem>, IWikiPageGenerator<TPage>
        where TPage : WikiPage
    {
        
        /// <inheritdoc/>
        internal WikiPageGenerator(WikiSite site) : base(site)
        {
        }

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
        /// Determins whether to remove duplicate page results generated from generator results.
        /// </summary>
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
        public IAsyncEnumerable<TPage> EnumPagesAsync(PageQueryOptions options)
        {
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when using generators.", nameof(options));
            var queryParams = RequestHelper.GetPageFetchingParams(options);
            queryParams.Add("generator", GeneratorName);
            foreach (var v in EnumGeneratorParameters())
                queryParams[v.Key] = v.Value;
            return RequestHelper.QueryWithContinuation(Site, queryParams, DistinctGeneratedPages)
                .SelectMany(jquery => WikiPage.FromJsonQueryResult(Site, jquery, options).Cast<TPage>()
                    .ToAsyncEnumerable());
        }
    }

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
