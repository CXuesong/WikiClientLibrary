using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// The traceable API request message to be sent to the wiki sites.
    /// </summary>
    public abstract class WikiRequestMessage
    {

        private static readonly long baseCounter =
            (long)(Environment.TickCount ^ RuntimeInformation.OSDescription.GetHashCode()) << 32;

        private static int idCounter;

        /// <param name="id">Id of the request, for tracing. If left <c>null</c>, an automatically-generated id will be used.</param>
        public WikiRequestMessage(string id)
        {
            Id = id ?? NextId();
        }

        private static string NextId()
        {
            var localCounter = Interlocked.Increment(ref idCounter);
            return (baseCounter | (uint)localCounter).ToString("X16");
        }

        /// <summary>
        /// Id of the request, for tracing.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the HTTP method used to send the request.
        /// </summary>
        public abstract HttpMethod GetHttpMethod();

        /// <summary>
        /// Gets the URI query part for the endpoint invocation.
        /// </summary>
        /// <value>The URI query part is the part of the request URI on the right-hand-side of
        /// the first question mark(<c>?</c>), or <c>null</c> if no question mark or query is appended
        /// to the endpoint URL.</value>
        /// <remarks>Returning <see cref="string.Empty"/> will cause a single question mark be appended to the
        /// endpoint URL when sending the request.</remarks>
        public abstract string GetHttpQuery();

        /// <summary>
        /// Gets the <see cref="HttpContent"/> corresponding to this message.
        /// </summary>
        public abstract HttpContent GetHttpContent();

        /// <inheritdoc />
        public override string ToString()
        {
            return Id;
        }

    }
}