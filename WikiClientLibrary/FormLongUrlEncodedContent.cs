using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    /// <summary>
    /// A container for name/value tuples encoded using application/x-www-form-urlencoded MIME type.
    /// This implementation solves issue #6, that an exception is thrown when the content is too long. 
    /// </summary>
    internal class FormLongUrlEncodedContent : ByteArrayContent
    {
        // as defined in System.Uri.c_MaxUriBufferSize, .NET 4.6.2
        public const int c_MaxUriBufferSize = 0xFFF0;

        // as defined in HttpRuleParser.DefaultHttpEncoding
        public static readonly Encoding DefaultHttpEncoding = Encoding.GetEncoding("iso-8859-1");

        public FormLongUrlEncodedContent(IEnumerable<KeyValuePair<string, string>> nameValueCollection)
          : base(GetContentByteArray(nameValueCollection))
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        }

        private static byte[] GetContentByteArray(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            if (pairs == null)
                throw new ArgumentNullException(nameof(pairs));
            var sb = new StringBuilder();
            foreach (var nameValue in pairs)
            {
                if (sb.Length > 0)
                    sb.Append('&');
                Encode(sb, nameValue.Key);
                sb.Append('=');
                Encode(sb, nameValue.Value);
            }
            sb.Replace("%20", "+");
            return DefaultHttpEncoding.GetBytes(sb.ToString());
        }

        private static void Encode(StringBuilder sb, string data)
        {
            const int partitionSize = c_MaxUriBufferSize - 10;
            if (string.IsNullOrEmpty(data)) return;
            for (int i = 0; i < data.Length; i += partitionSize)
            {
                var ps = Math.Min(partitionSize, data.Length - i);
                sb.Append(Uri.EscapeDataString(data.Substring(i, ps)));
            }
        }
    }
}
