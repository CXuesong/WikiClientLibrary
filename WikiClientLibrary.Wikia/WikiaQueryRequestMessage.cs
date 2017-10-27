using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia
{
    /// <summary>
    /// The request message used with Wikia publicized and non-public API endpoints.
    /// </summary>
    /// <remarks>
    /// The requests are issued with HTTP GET method. The fiedls are concatenated after the endpoint
    /// URL as URI query part (e.g. <c>endpointUrl?query</c>).
    /// </remarks>
    public class WikiaQueryRequestMessage : WikiRequestMessage
    {

        private static readonly KeyValuePair<string, object>[] emptyReadonlyFields = { };

        private readonly IList<KeyValuePair<string, object>> fields;
        private IList<KeyValuePair<string, object>> readonlyFields;
        private string queryString;

        /// <summary>
        /// Initializes a <see cref="WikiaQueryRequestMessage"/> instance with
        /// the automatically-generated message ID and empty query fields.
        /// </summary>
        /// <remarks>Note that you cannot insert fields after initialization.</remarks>
        public WikiaQueryRequestMessage() : this(null, null)
        {
        }
        
        /// <inheritdoc cref="WikiaQueryRequestMessage(string,object,bool)"/>
        public WikiaQueryRequestMessage(object dict) : this(null, dict, false)
        {
        }
        
        /// <inheritdoc cref="WikiaQueryRequestMessage(string,object,bool)"/>
        public WikiaQueryRequestMessage(object dict, bool httpPost) : this(null, dict, httpPost)
        {
        }

        /// <inheritdoc cref="WikiaQueryRequestMessage(string,object,bool)"/>
        public WikiaQueryRequestMessage(string id, object fieldCollection) : this(id, fieldCollection, false)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a <see cref="WikiaQueryRequestMessage"/> instance with
        /// the message ID and query fields.
        /// </summary>
        /// <param name="fieldCollection">
        /// A dictionary or anonymous object containing the key-value pairs.
        /// See <see cref="MediaWikiHelper.EnumValues"/> for more information.
        /// For queries without query part, you can set this parameter to <c>null</c>.
        /// </param>
        /// <param name="httpPost">Whether to use HTTP POST method to issue the request.</param>
        public WikiaQueryRequestMessage(string id, object fieldCollection, bool httpPost) : base(id)
        {
            if (fieldCollection == null)
            {
                fields = null;
                readonlyFields = emptyReadonlyFields;
            }
            else
            {
                fields = MediaWikiHelper.EnumValues(fieldCollection).ToList();
            }
            UseHttpPost = httpPost;
        }

        /// <summary>
        /// Gets a value that indicates whether the message should be sent via HTTP POST instead of HTTP GET.
        /// </summary>
        public bool UseHttpPost { get; }

        /// <inheritdoc />
        public override HttpMethod GetHttpMethod() => UseHttpPost ? HttpMethod.Post : HttpMethod.Get;
        
        /// <inheritdoc />
        public override string GetHttpQuery()
        {
            if (UseHttpPost || fields == null) return null;
            if (queryString != null) return queryString;
            var sb = new StringBuilder();
            foreach (var p in fields)
            {
                if (p.Value == null) continue;
                if (sb.Length > 0) sb.Append('&');
                // This encodes space(' ') to '+'
                sb.Append(WebUtility.UrlEncode(p.Key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(p.Value?.ToString()));
            }
            var qs = sb.ToString();
            Volatile.Write(ref queryString, qs);
            return qs;
        }

        /// <summary>
        /// Gets a readonly list of all the fields in the form.
        /// </summary>
        public IList<KeyValuePair<string, object>> Fields
        {
            get
            {
                if (readonlyFields != null) return readonlyFields;
                var local = new ReadOnlyCollection<KeyValuePair<string, object>>(fields);
                Volatile.Write(ref readonlyFields, local);
                return readonlyFields;
            }
        }

        /// <inheritdoc />
        public override HttpContent GetHttpContent()
        {
            if (!UseHttpPost) return null;
            return new FormUrlEncodedContent(fields.Where(p => p.Value != null)
                .Select(p => new KeyValuePair<string, string>(p.Key, p.Value?.ToString())));
        }
    }
}
