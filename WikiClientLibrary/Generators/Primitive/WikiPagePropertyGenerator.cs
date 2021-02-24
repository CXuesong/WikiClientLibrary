﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators.Primitive
{
    /// <summary>
    /// Represents a page-property-based MediaWiki generator
    /// (or sequence, see <a href="https://www.mediawiki.org/wiki/API:Generator">mw:API:Generator</a>)
    /// of <see cref="WikiPage"/>.
    /// </summary>
    /// <typeparam name="TItem">The type of listed items.</typeparam>
    /// <remarks>
    /// <para>For common implementations for MediaWiki generator, prefer inheriting from
    /// <see cref="WikiPagePropertyGenerator"/>, which returns <see cref="WikiPageStub"/>
    /// for <see cref="WikiList{T}.EnumItemsAsync"/> implementation.</para>
    /// <para>
    /// For <c>generator</c>s, it's not necessary for the sequence taken out
    /// from <see cref="EnumPagesAsync(IWikiPageQueryProvider)"/> to be kept ordered.
    /// If you need ordered sequence, for example,
    /// enumerating the links from the beginning of an article using <see cref="LinksGenerator"/>,
    /// use <see cref="WikiPagePropertyList{T}.EnumItemsAsync"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WikiPageGenerator{TItem}"/>
    public abstract class WikiPagePropertyGenerator<TItem> : WikiPagePropertyList<TItem>, IWikiPageGenerator
    {
        /// <inheritdoc/>
        protected WikiPagePropertyGenerator(WikiSite site) : base(site)
        {
        }

        public virtual string GeneratorName => PropertyName;

        /// <summary>
        /// Fills generator parameters for <c>action=query&amp;generator=</c> request.
        /// </summary>
        /// <returns>A sequence of fields, which will override the basic query parameters.</returns>
        public virtual IEnumerable<KeyValuePair<string, object?>> EnumGeneratorParameters()
        {
            return EnumListParameters().Select(p => new KeyValuePair<string, object?>("g" + p.Key, p.Value));
        }

        /// <summary>
        /// When using the default implementation of <see cref="EnumPagesAsync(IWikiPageQueryProvider)"/>,
        /// determines whether to remove duplicate page results generated from generator results.
        /// </summary>
        /// <remarks>If the derived class has overridden <see cref="EnumPagesAsync(IWikiPageQueryProvider)"/>,
        /// the value of this property might be ignored.</remarks>
        protected virtual bool DistinctGeneratedPages => false;

        /// <summary>
        /// Asynchronously generate the sequence of pages.
        /// </summary>
        public virtual IAsyncEnumerable<WikiPage> EnumPagesAsync()
        {
            return EnumPagesAsync(MediaWikiHelper.QueryProviderFromOptions(PageQueryOptions.None));
        }

        /// <summary>
        /// Asynchronously generates the sequence of pages.
        /// </summary>
        public virtual IAsyncEnumerable<WikiPage> EnumPagesAsync(PageQueryOptions options)
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
            queryParams.Add("titles", PageTitle);
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
    /// <see cref="WikiPagePropertyGenerator{TItem}"/>, where <c>TItem</c> is <see cref="WikiPageStub"/>.
    /// </summary>
    /// <remarks>
    /// <para>For a more generic implementations for page-property-based MediaWiki generator,
    /// or implementations returning a sequence where the item type is not <see cref="WikiPageStub"/>,
    /// please derive your generator from <see cref="WikiPagePropertyGenerator{TItem}"/>.</para>
    /// </remarks>
    public abstract class WikiPagePropertyGenerator : WikiPagePropertyGenerator<WikiPageStub>
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
