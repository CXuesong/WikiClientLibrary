using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WikiClientLibrary
{
    internal static class Utility
    {
 /// <summary>
        /// Convert name-value paris to URL query format.
        /// This overload handles <see cref="ExpandoObject"/> as well as anonymous objects.
        /// </summary>
        public static string EncodeValuePairs(object values)
        {
            var pc = values as IEnumerable<KeyValuePair<string, string>>;
            if (pc != null) return EncodeValuePairs(pc);
            return EncodeValuePairs(from p in values.GetType().GetRuntimeProperties()
                select new KeyValuePair<string, string>(p.Name, Convert.ToString(p.GetValue(values))));
        }

        /// <summary>
        /// Convert name-value paris to URL query format.
        /// </summary>
        public static string EncodeValuePairs(IEnumerable<KeyValuePair<string, string>> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var sb = new StringBuilder();
            var isFirst = true;
            foreach (var v in values)
            {
                if (isFirst) isFirst = false;
                else sb.Append('&');
                sb.Append(WebUtility.UrlEncode(v.Key));
                if (v.Value != null)
                {
                    sb.Append("=");
                    sb.Append(WebUtility.UrlEncode(v.Value));
                }
            }
            return sb.ToString();
        }

        public static void SetHeader(this HttpWebRequest request, string header, string value)
        {
            // Retrieve the property through reflection.
            var PropertyInfo = request.GetType().GetRuntimeProperty(header.Replace("-", string.Empty));
            // Check if the property is available.
            if (PropertyInfo != null)
            {
                // Set the value of the header.
                PropertyInfo.SetValue(request, value, null);
            }
            else
            {
                // Set the value of the header.
                request.Headers[header] = value;
            }
        }
    }
}
