using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a set of wiki <see cref="Site"/> instances, identified by their names (often the same as interwiki prefix).
    /// </summary>
    /// <remarks>The wiki names here should be case-insensitive. For interwiki prefixes, the names are often lower-case.</remarks>
    public interface IFamily
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
        /// Asynchronously gets a <see cref="Site"/> instance from the specified family name.
        /// </summary>
        /// <param name="prefix">The member name in the family. Usually this is the interwiki prefix.</param>
        /// <returns>A site instance, or <c>null</c> if no site with the specified family name found..</returns>
        /// <remarks>The implementation should be thread-safe, if multiple threads are to use this instance with other classes.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
        Task<Site> GetSiteAsync(string prefix);
    }

    /// <summary>
    /// Provides a simple <see cref="IFamily"/> implementation based on
    /// a list of API endpoint URLs.
    /// </summary>
    public class Family : IFamily, IReadOnlyCollection<string>
    {

        public WikiClientBase WikiClient { get; }

        private class SiteEntry
        {
            private volatile Task<Site> _Task;

            public SiteEntry(string prefix, string apiEndpoint)
            {
                Debug.Assert(prefix != null);
                Debug.Assert(apiEndpoint != null);
                Prefix = prefix;
                ApiEndpoint = apiEndpoint;
            }

            public string Prefix { get; }

            public string ApiEndpoint { get; }

            public Task<Site> Task
            {
                get { return _Task; }
                set { _Task = value; }
            }
        }

        private readonly Dictionary<string, SiteEntry> sites = new Dictionary<string, SiteEntry>();

        /// <summary>
        /// Initializes the instance with a <see cref="Client.WikiClient"/> and family name.
        /// </summary>
        public Family(WikiClient wikiClient) : this(wikiClient, null)
        {
        }

        /// <summary>
        /// Initializes the instance with a <see cref="Client.WikiClient"/> and family name.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> is <c>null</c>.</exception>
        public Family(WikiClient wikiClient, string name)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            WikiClient = wikiClient;
            Name = name;
        }

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
            SiteEntry entry;
            if (sites.TryGetValue(prefix.ToLower(), out entry))
                return entry.Prefix;
            return null;
        }

        /// <summary>
        /// Asynchronously gets a <see cref="Site"/> instance from the specified family name. (Case-insensitive)
        /// This method will create the Site instance, if necessary; otherwise it will return the created one.
        /// </summary>
        /// <param name="prefix">The member name in the family. Usually this is the interwiki prefix.</param>
        /// <returns>A site instance, or <c>null</c> if no site with the specified family name found..</returns>
        /// <remarks>The implementation should be thread-safe, if multiple threads are to use this instance with other classes.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
        public Task<Site> GetSiteAsync(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            prefix = prefix.ToLower();
            SiteEntry entry;
            if (sites.TryGetValue(prefix, out entry))
            {
                var task = entry.Task;
                if (task != null) return task;
                lock (entry)
                {
                    task = entry.Task;
                    if (task == null)
                        entry.Task = task = Site.CreateAsync(WikiClient, entry.ApiEndpoint);
                    return task;
                }
            }
            return Task.FromResult((Site) null);
        }

        /// <inheritdoc />
        public IEnumerator<string> GetEnumerator() => sites.Keys.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public int Count => sites.Count;
    }
}
