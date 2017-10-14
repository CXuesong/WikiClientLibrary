using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Newtonsoft.Json;

namespace WikiClientLibrary.Wikibase.DataTypes
{
    /// <summary>
    /// An atomic instance of URI used in Wikibase.
    /// </summary>
    [JsonObject(ItemConverterType = typeof(WikibaseUriJsonConverter))]
    public sealed class WbUri : IEquatable<WbUri>
    {

        private static readonly ConcurrentDictionary<string, WeakReference<WbUri>> cacheDict =
            new ConcurrentDictionary<string, WeakReference<WbUri>>();

        public static WbUri Get(string uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            WbUri inst = null;
            // Fast route
            if (cacheDict.TryGetValue(uri, out var r) && r.TryGetTarget(out inst))
                return inst;
            // Slow route
            cacheDict.AddOrUpdate(uri,
                u => new WeakReference<WbUri>(inst = new WbUri(u)),
                (u, r0) =>
                {
                    if (!r0.TryGetTarget(out inst))
                    {
                        inst = new WbUri(u);
                        return new WeakReference<WbUri>(inst);
                    }
                    return r0;
                });
            return inst;
        }

        private WbUri(string uri)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        /// <summary>Gets the string of referenced URI.</summary>
        public string Uri { get; }

        /// <summary>Gets the referenced URI.</summary>
        public override string ToString() => Uri;

        /// <inheritdoc />
        public bool Equals(WbUri other)
        {
            Debug.Assert(ReferenceEquals(this, other) == (this.Uri == other.Uri));
            return ReferenceEquals(this, other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
#if DEBUG
            if (obj is WbUri other)
                Debug.Assert(ReferenceEquals(this, other) == (this.Uri == other.Uri));
#endif
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Uri.GetHashCode();
        }

        public static bool operator ==(WbUri left, WbUri right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(WbUri left, WbUri right)
        {
            return !Equals(left, right);
        }

        public static implicit operator WbUri(string s)
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
            var obj = (WbUri) value;
            writer.WriteValue(obj.Uri);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String) throw new JsonException("Expect string value.");
            var uri = (string) reader.Value;
            return WbUri.Get(uri);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(WbUri);
        }
    }

}
