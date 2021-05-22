// Enables the following conditional switch here
// to prevent test cases from making any edits.

//#define DRY_RUN

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using WikiClientLibrary.Wikia;
using WikiClientLibrary.Wikia.Sites;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1
{

    public class UnitTestsBase : IAsyncDisposable
    {

        public UnitTestsBase(ITestOutputHelper output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            Output = output;
            OutputLoggerFactory = new LoggerFactory();
            OutputLoggerFactory.AddProvider(new TestOutputLoggerProvider(Output));
        }

        public LoggerFactory OutputLoggerFactory { get; }

        public ITestOutputHelper Output { get; }
        
        protected void WriteOutput(object? value)
        {
            WriteOutput(value == null ? "<null>" : value.ToString());
        }

        protected void WriteOutput(string? message)
        {
            Output.WriteLine(message);
        }

        protected void WriteOutput(string format, params object?[] args)
        {
            WriteOutput(string.Format(format, args));
        }

        protected void ShallowTrace(object? obj, int depth = 2)
        {
            var rawTrace = Utility.DumpObject(obj, depth);
#if ENV_CI_BUILD
            // We don't want to abuse CI logs.
            const int MAX_TRACE_LENGTH = 5000;
            if (rawTrace.Length > MAX_TRACE_LENGTH)
            {
                rawTrace = rawTrace.Substring(0, MAX_TRACE_LENGTH) + "… [+" + (rawTrace.Length - MAX_TRACE_LENGTH) + " chars]";
            }
#endif
            Output.WriteLine(rawTrace);
        }

        protected virtual ValueTask DisposeAsync(bool disposing) => default;

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true);
            GC.SuppressFinalize(this);
        }
    }

    public class WikiSiteTestsBase : UnitTestsBase
    {

        public WikiSiteTestsBase(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output)
        {
            WikiSiteProvider = wikiSiteProvider;
        }

        protected WikiSiteProvider WikiSiteProvider { get; }

        /// <summary>
        /// Creates a <see cref="WikiSite"/> instance with a dedicated <see cref="WikiClient"/> instance.
        /// </summary>
        /// <remarks>This method can be handy for you to maul a certain WikiClient without affecting other WikiSite instances.</remarks>
        protected Task<WikiSite> CreateIsolatedWikiSiteAsync(string apiEndpoint)
        {
            var isolatedClient = WikiSiteProvider.CreateWikiClient();
            return WikiSiteProvider.CreateWikiSiteAsync(isolatedClient, apiEndpoint, OutputLoggerFactory);
        }

        /// <summary>
        /// Create or get a wiki site from local cache.
        /// </summary>
        public Task<WikiSite> GetWikiSiteAsync(string endpointUrl) => WikiSiteProvider.GetWikiSiteAsync(endpointUrl, OutputLoggerFactory);



        /// <summary>
        /// Asserts that modifications to wiki site can be done in unit tests.
        /// </summary>
        protected static void AssertModify()
        {
#if DRY_RUN
            throw new SkipException("Remove #define DRY_RUN to perform edit tests.");
#endif
        }

        protected Task<WikiSite> WpEnSiteAsync => GetWikiSiteAsync(Endpoints.WikipediaEn);

        protected Task<WikiSite> WpTest2SiteAsync => GetWikiSiteAsync(Endpoints.WikipediaTest2);

        protected Task<WikiaSite> WikiaTestSiteAsync
        {
            get
            {
                async Task<WikiaSite> Cast()
                {
                    return (WikiaSite)await GetWikiSiteAsync(Endpoints.WikiaTest);
                }

                return Cast();
            }
        }

        protected Task<WikiSite> TFWikiSiteAsync => GetWikiSiteAsync(Endpoints.TFWiki);

        protected Task<WikiSite> WpLzhSiteAsync => GetWikiSiteAsync(Endpoints.WikipediaLzh);

        protected Task<WikiSite> WpBetaSiteAsync => GetWikiSiteAsync(Endpoints.WikipediaBetaEn);

        protected Task<WikiSite> WikimediaCommonsBetaSiteAsync => GetWikiSiteAsync(Endpoints.WikimediaCommonsBeta);

        protected Task<WikiSite> WikidataSiteAsync => GetWikiSiteAsync(Endpoints.Wikidata);

        protected Task<WikiSite> WikidataTestSiteAsync => GetWikiSiteAsync(Endpoints.WikidataTest);

        protected Task<WikiSite> WikiSiteFromNameAsync(string sitePropertyName)
        {
            static async Task<TDest> CastAsync<TSource, TDest>(Task<TSource> sourceTask)
                where TSource : class
                where TDest : class
            {
                return (TDest)(object)await sourceTask;
            }

            var task = GetType()
                .GetProperty(sitePropertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                !.GetValue(this);
            if (task is Task<WikiSite> ws) return ws;
            if (task is Task<WikiaSite> was) return CastAsync<WikiaSite, WikiSite>(was);
            throw new NotSupportedException();
        }

        protected WikiClient CreateWikiClient()
        {
            var client = WikiSiteProvider.CreateWikiClient();
            client.Logger = OutputLoggerFactory.CreateLogger<WikiClient>();
            return client;
        }

    }
}
