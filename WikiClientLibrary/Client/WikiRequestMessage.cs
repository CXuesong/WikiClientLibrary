using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// The traceable MediaWiki API request message.
    /// </summary>
    public abstract class WikiRequestMessage
    {

        private static readonly long baseCounter =
            (long)(Environment.TickCount ^ RuntimeInformation.OSDescription.GetHashCode()) << 32;

        private static int idCounter;

        public WikiRequestMessage(string id)
        {
            Id = id ?? NextId();
        }

        private static string NextId()
        {
            var localCounter = Interlocked.Increment(ref idCounter);
            return (baseCounter | (uint) localCounter).ToString("X16");
        }

        /// <summary>
        /// Id of the request. For tracing.
        /// </summary>
        public string Id { get; }

        public abstract HttpContent GetHttpContent();

        /// <inheritdoc />
        public override string ToString()
        {
            return Id;
        }
    }

    public sealed class WikiFormRequestMessage : WikiRequestMessage
    {

        private readonly IDictionary<string, object> fieldDict;
        private IDictionary<string, object> readonlyFieldDict;

        public WikiFormRequestMessage(object fieldCollection) : this(null, null, fieldCollection, false)
        {
        }

        public WikiFormRequestMessage(object fieldCollection, bool forceMultipartFormData) : this(null, null,
            fieldCollection, forceMultipartFormData)
        {
        }

        public WikiFormRequestMessage(string id, object fieldCollection) : this(id, null, fieldCollection, false)
        {
        }

        public WikiFormRequestMessage(string id, WikiFormRequestMessage baseForm,
            object fieldCollection, bool forceMultipartFormData) : base(id)
        {
            if (baseForm == null)
                fieldDict = new Dictionary<string, object>();
            else
                fieldDict = new Dictionary<string, object>(baseForm.fieldDict);
            // Override values.
            foreach (var p in Utility.EnumValues(fieldCollection))
                fieldDict[p.Key] = p.Value;
            if (forceMultipartFormData || (baseForm?.AsMultipartFormData ?? false)) AsMultipartFormData = true;
            else AsMultipartFormData = this.fieldDict.Any(p => p.Value is Stream);
        }

        public bool AsMultipartFormData { get; }

        public IDictionary<string, object> Fields
        {
            get
            {
                if (readonlyFieldDict != null) return readonlyFieldDict;
                var local = new ReadOnlyDictionary<string, object>(fieldDict);
                Volatile.Write(ref readonlyFieldDict, local);
                return local;
            }
        }

        public object GetValue(string key)
        {
            if (fieldDict.TryGetValue(key, out var v)) return v;
            return null;
        }

        public override HttpContent GetHttpContent()
        {
            if (AsMultipartFormData)
            {
                var content = new MultipartFormDataContent();
                foreach (var p in fieldDict)
                {
                    switch (p.Value)
                    {
                        case string s:
                            content.Add(new StringContent(s), p.Key);
                            break;
                        case Stream stream:
                            content.Add(new KeepAlivingStreamContent(stream), p.Key, "dummy");
                            break;
                        case null:
                            // Ignore null entries.
                            break;
                        default:
                            var stringValue = Utility.ToWikiQueryValue(p.Value);
                            if (stringValue != null)
                                content.Add(new StringContent(stringValue), p.Key);
                            break;
                    }
                }
                return content;
            }
            else
            {
                return new FormLongUrlEncodedContent(Utility.ToWikiStringValuePairs(fieldDict)
                    .Select(p => new KeyValuePair<string, string>(p.Key, Utility.ToWikiQueryValue(p.Value)))
                    .Where(p => p.Value != null));
            }
        }
    }

    public sealed class WikiRawRequestMessage : WikiRequestMessage
    {

        private readonly HttpContent httpContent;

        /// <inheritdoc />
        public WikiRawRequestMessage(string id, HttpContent httpContent) : base(id)
        {
            this.httpContent = httpContent ?? throw new ArgumentNullException(nameof(httpContent));
        }

        /// <inheritdoc />
        public override HttpContent GetHttpContent()
        {
            // appendedValues will be silently ignored.
            return httpContent;
        }
    }

}
