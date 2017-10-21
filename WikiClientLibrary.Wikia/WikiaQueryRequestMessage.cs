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
        public WikiaQueryRequestMessage() : this(null, null)
        {
        }

        /// <inheritdoc />
        public WikiaQueryRequestMessage(object dict) : this(null, dict)
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
        public WikiaQueryRequestMessage(string id, object fieldCollection) : base(id)
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
        }

        /// <inheritdoc />
        public override HttpMethod GetHttpMethod() => HttpMethod.Get;

        /// <inheritdoc />
        public override string GetHttpQuery()
        {
            if (fields == null) return null;
            if (queryString != null) return queryString;
            var sb = new StringBuilder();
            foreach (var p in fields)
            {
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
            return null;
        }
    }
}
