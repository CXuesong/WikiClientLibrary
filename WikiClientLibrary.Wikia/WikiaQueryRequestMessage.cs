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

        private readonly IList<KeyValuePair<string, object>> fields;
        private IList<KeyValuePair<string, object>> readonlyFields;
        private string queryString;

        /// <inheritdoc />
        public WikiaQueryRequestMessage(object dict) : this(null, dict)
        {
        }

        /// <inheritdoc />
        public WikiaQueryRequestMessage(string id, object dict) : base(id)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            fields = MediaWikiHelper.EnumValues(dict).ToList();
        }

        /// <inheritdoc />
        public override HttpMethod GetHttpMethod() => HttpMethod.Get;

        /// <inheritdoc />
        public override string GetHttpQuery()
        {
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
