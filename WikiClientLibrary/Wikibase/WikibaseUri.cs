using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// An atomic instance of URI used in Wikibase.
    /// </summary>
    [JsonObject(ItemConverterType = typeof(WikibaseUriJsonConverter))]
    public sealed class WikibaseUri : IEquatable<WikibaseUri>
    {

        private static readonly ConcurrentDictionary<string, WeakReference<WikibaseUri>> cacheDict =
            new ConcurrentDictionary<string, WeakReference<WikibaseUri>>();

        public static WikibaseUri Get(string uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            WikibaseUri inst = null;
            // Fast route
            if (cacheDict.TryGetValue(uri, out var r) && r.TryGetTarget(out inst))
                return inst;
            // Slow route
            cacheDict.AddOrUpdate(uri,
                u => new WeakReference<WikibaseUri>(inst = new WikibaseUri(u)),
                (u, r0) =>
                {
                    if (!r0.TryGetTarget(out inst))
                    {
                        inst = new WikibaseUri(u);
                        return new WeakReference<WikibaseUri>(inst);
                    }
                    return r0;
                });
            return inst;
        }

        private WikibaseUri(string uri)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        /// <summary>Gets the string of referenced URI.</summary>
        public string Uri { get; }

        /// <summary>Gets the referenced URI.</summary>
        public override string ToString() => Uri;

        /// <inheritdoc />
        public bool Equals(WikibaseUri other)
        {
            Debug.Assert(ReferenceEquals(this, other) == (this.Uri == other.Uri));
            return ReferenceEquals(this, other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
#if DEBUG
            if (obj is WikibaseUri other)
                Debug.Assert(ReferenceEquals(this, other) == (this.Uri == other.Uri));
#endif
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Uri.GetHashCode();
        }

        public static bool operator ==(WikibaseUri left, WikibaseUri right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(WikibaseUri left, WikibaseUri right)
        {
            return !Equals(left, right);
        }

        public static implicit operator WikibaseUri(string s)
        {
            if (s == null) return null;
            return Get(s);
        }

    }

    internal class WikibaseUriJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var obj = (WikibaseUri) value;
            writer.WriteValue(obj.Uri);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String) throw new JsonException("Expect string value.");
            var uri = (string) reader.Value;
            return WikibaseUri.Get(uri);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(WikibaseUri);
        }
    }

}
