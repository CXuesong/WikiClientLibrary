using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// A container for name/value tuples encoded using application/x-www-form-urlencoded MIME type.
/// This implementation solves issue #6, that an exception is thrown when the content is too long. 
/// </summary>
internal class FormLongUrlEncodedContent : ByteArrayContent
{

    // as defined in HttpRuleParser.DefaultHttpEncoding
    public static readonly Encoding DefaultHttpEncoding = Encoding.GetEncoding("iso-8859-1");

    public FormLongUrlEncodedContent(IEnumerable<KeyValuePair<string, string?>> nameValueCollection)
        : base(GetContentByteArray(nameValueCollection))
    {
            Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        }

    private static byte[] GetContentByteArray(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
            if (pairs == null)
                throw new ArgumentNullException(nameof(pairs));
            var sb = new StringBuilder();
            foreach (var nameValue in pairs)
            {
                if (sb.Length > 0)
                    sb.Append('&');

                sb.Append(HttpUtility.UrlEncode(nameValue.Key));
                sb.Append('=');
                sb.Append(HttpUtility.UrlEncode(nameValue.Value));
            }

            return DefaultHttpEncoding.GetBytes(sb.ToString());
        }

}