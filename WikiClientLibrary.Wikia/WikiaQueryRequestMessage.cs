using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia
{
    public class WikiaQueryRequestMessage : WikiRequestMessage
    {

        private readonly string queryString;

        /// <inheritdoc />
        public WikiaQueryRequestMessage(string id, object dict) : base(id)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            var sb = new StringBuilder();
            foreach (var p in MediaWikiHelper.EnumValues(dict))
            {
                if (sb.Length > 0) sb.Append('&');
                // This encodes space(' ') to '+'
                sb.Append(WebUtility.UrlEncode(p.Key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(p.Value?.ToString()));
            }
            queryString = sb.ToString();
        }

        /// <inheritdoc />
        public override HttpMethod GetHttpMethod() => HttpMethod.Get;

        /// <inheritdoc />
        public override string GetHttpQuery() => queryString;

        /// <inheritdoc />
        public override HttpContent GetHttpContent()
        {
            return null;
        }
    }
}
