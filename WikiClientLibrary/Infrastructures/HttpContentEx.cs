﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace WikiClientLibrary.Infrastructures
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

    /// <summary>
    /// An adapter of <see cref="StreamContent"/> that won't automatically dispose its underlying stream.
    /// </summary>
    internal sealed class KeepAlivingStreamContent : StreamContent
    {

        public KeepAlivingStreamContent(Stream stream) : base(stream)
        {
        }

        public KeepAlivingStreamContent(Stream stream, int bufferSize) : base(stream, bufferSize)
        {

        }

#if BCL_FEATURE_DISPOSE_HTTPCONTENT

        // Workaround for https://github.com/dotnet/corefx/pull/19082
        // This PR hasn't been merged until .NET Core 2.0.

        private static readonly Action<KeepAlivingStreamContent> disposeImpl; // Calls System.Net.Http.HttpContent.Dispose

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                disposeImpl(this); // We want to bypass StreamContent::Dispose(true) implementation.
            else
                base.Dispose(false);
        }

        static KeepAlivingStreamContent()
        {
            var method = new DynamicMethod("$KeepAlivingStreamContent.Dispose()", null,
                new[] { typeof(KeepAlivingStreamContent) }, typeof(KeepAlivingStreamContent));
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Ldc_I4_1); // disposing
            il.EmitCall(OpCodes.Call,
                typeof(HttpContent).GetTypeInfo().DeclaredMethods
                    .First(m => m.Name == "Dispose"
                                && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(bool)),
                null);
            il.Emit(OpCodes.Ret);
            disposeImpl =
                (Action<KeepAlivingStreamContent>)method.CreateDelegate(typeof(Action<KeepAlivingStreamContent>));
        }

#endif

    }
}