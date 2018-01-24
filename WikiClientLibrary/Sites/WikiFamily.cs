using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Represents a set of wiki <see cref="WikiSite"/> instances, identified by their names (often the same as interwiki prefix).
    /// </summary>
    /// <remarks>The wiki names here should be case-insensitive. For interwiki prefixes, the names are often lower-case.</remarks>
    public interface IWikiFamily
    {
        /// <summary>
        /// Gets the name of this wiki family.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Tries to normalize the specified wiki prefix by changing the letter-case of the input name.
        /// </summary>
        /// <param name="prefix">The member name in the family. Usually this is the interwiki prefix.</param>
        /// <returns>The normalized wiki name, if the specified name exists. Otherwise <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
        string TryNormalize(string prefix);

        /// <summary>
        /// Asynchronously gets a <see cref="WikiSite"/> instance from the specified family name.
        /// </summary>
        /// <param name="prefix">The member name in the family. Usually this is the interwiki prefix.</param>
        /// <returns>A site instance, or <c>null</c> if no site with the specified family name found..</returns>
        /// <remarks>The implementation should be thread-safe, if multiple threads are to use this instance with other classes.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
        Task<WikiSite> GetSiteAsync(string prefix);
    }

    /// <summary>
    /// Provides arguments for <see cref="WikiFamily.SiteCreated"/> event.
    /// </summary>
    public class WikiFamilySiteCreatedEventArgs : EventArgs
    {
        public WikiFamilySiteCreatedEventArgs(string prefix, WikiSite site)
        {
            Prefix = prefix;
            Site = site ?? throw new ArgumentNullException(nameof(site));
        }

        /// <summary>Interwiki prefix of the site.</summary>
        public string Prefix { get; }

        /// <summary><see cref="WikiSite"/> instance of the site.</summary>
        public WikiSite Site { get; }

    }

    /// <summary>
    /// Provides a simple <see cref="IWikiFamily"/> implementation based on
    /// a list of API endpoint URLs.
    /// </summary>
    public class WikiFamily : IWikiFamily, IWikiClientLoggable, IReadOnlyCollection<string>
    {

        private readonly Dictionary<string, SiteEntry> sites = new Dictionary<string, SiteEntry>();
        private ILogger _Logger = NullLogger.Instance;

        /// <summary>
        /// Raised when a new <see cref="WikiSite"/> has been instantiated, but before it is
        /// stored into the site cache.
        /// </summary>
        public event EventHandler<WikiFamilySiteCreatedEventArgs> SiteCreated;

        /// <summary>
        /// Initializes the instance with a <see cref="Client.WikiClient"/> and family name.
        /// </summary>
        public WikiFamily(IWikiClient wikiClient) : this(wikiClient, null)
        {
        }

        /// <summary>
        /// Initializes the instance with a <see cref="Client.WikiClient"/> and family name.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> is <c>null</c>.</exception>
        public WikiFamily(IWikiClient wikiClient, string name)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            WikiClient = wikiClient;
            Name = name;
        }

        public IWikiClient WikiClient { get; }

        /// <summary>
        /// Add a new wiki site into this family.
        /// </summary>
        /// <param name="prefix">The member name in the family. Usually this is the interwiki prefix.</param>
        /// <param name="apiEndPoint">The endpoint URL.</param>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> or <paramref name="apiEndPoint"/> is <c>null</c>.</exception>
        public void Register(string prefix, string apiEndPoint)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            if (apiEndPoint == null) throw new ArgumentNullException(nameof(apiEndPoint));
            sites.Add(prefix.ToLower(), new SiteEntry(prefix, apiEndPoint));
        }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string TryNormalize(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            if (sites.TryGetValue(prefix.ToLower(), out var entry))
                return entry.Prefix;
            return null;
        }

        /// <summary>
        /// Asynchronously gets a <see cref="WikiSite"/> instance from the specified family name. (Case-insensitive)
        /// This method will create the Site instance, if necessary; otherwise it will return the created one.
        /// </summary>
        /// <param name="prefix">The member name in the family. Usually this is the interwiki prefix.</param>
        /// <returns>A site instance, or <c>null</c> if no site with the specified family name found..</returns>
        /// <remarks>The implementation should be thread-safe, if multiple threads are to use this instance with other classes.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
        public Task<WikiSite> GetSiteAsync(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            prefix = prefix.ToLower();
            if (sites.TryGetValue(prefix, out var entry))
            {
                var task = entry.Task;
                if (task != null) return task;
                lock (entry)
                {
                    task = entry.Task;
                    if (task == null)
                        entry.Task = task = CreateSiteAsync(entry.Prefix, entry.ApiEndpoint);
                    return task;
                }
            }
            return Task.FromResult((WikiSite) null);
        }

        /// <summary>
        /// Asynchronously create a <see cref="WikiSite"/> instance.
        /// </summary>
        /// <param name="prefix">Site prefix, as is registered.</param>
        /// <param name="apiEndpoint">Site API endpoint URL.</param>
        /// <returns>A <see cref="WikiSite"/> instance.</returns>
        protected virtual async Task<WikiSite> CreateSiteAsync(string prefix, string apiEndpoint)
        {
            var site = new WikiSite(WikiClient, apiEndpoint);
            await site.Initialization;
            Logger.LogTrace("[[{Family}:{prefix}:]] has been instantiated.", Name, prefix);
            OnSiteCreated(new WikiFamilySiteCreatedEventArgs(prefix, site));
            return site;
        }

        /// <inheritdoc />
        public IEnumerator<string> GetEnumerator() => sites.Keys.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public int Count => sites.Count;

        /// <inheritdoc />
        /// <remarks>
        /// This property only affects the logger used for the <see cref="WikiFamily"/> instance.
        /// The created <see cref="WikiSite"/> instances will still use <see cref="NullLogger"/> by default.
        /// </remarks>
        public ILogger Logger
        {
            get => _Logger;
            set => _Logger = value ?? NullLogger.Instance;
        }
        
        /// <summary>
        /// Raises <see cref="SiteCreated"/> event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnSiteCreated(WikiFamilySiteCreatedEventArgs e)
        {
            SiteCreated?.Invoke(this, e);
        }

        private class SiteEntry
        {
            private volatile Task<WikiSite> _Task;

            public SiteEntry(string prefix, string apiEndpoint)
            {
                Debug.Assert(prefix != null);
                Debug.Assert(apiEndpoint != null);
                Prefix = prefix;
                ApiEndpoint = apiEndpoint;
            }

            public string Prefix { get; }

            public string ApiEndpoint { get; }

            public Task<WikiSite> Task
            {
                get => _Task;
                set => _Task = value;
            }
        }
    }
}
