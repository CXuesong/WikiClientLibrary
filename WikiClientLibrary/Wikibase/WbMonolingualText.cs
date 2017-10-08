using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Wikibase
{
    public struct WbMonolingualText : IEquatable<WbMonolingualText>
    {

        public static readonly WbMonolingualText Empty = new WbMonolingualText();

        public WbMonolingualText(string language, string text)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            if (text == null) throw new ArgumentNullException(nameof(text));
            // Simple normalization.
            Language = language.Trim().ToLowerInvariant();
            Text = text;
        }

        public string Language { get; }

        public string Text { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return "[" + Language + "]" + Text;
        }

        /// <inheritdoc />
        public bool Equals(WbMonolingualText other)
        {
            return string.Equals(Text, other.Text) && string.Equals(Language, other.Language);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WbMonolingualText text && Equals(text);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Text != null ? Text.GetHashCode() : 0) * 397) ^ (Language != null ? Language.GetHashCode() : 0);
            }
        }

        public static bool operator ==(WbMonolingualText left, WbMonolingualText right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WbMonolingualText left, WbMonolingualText right)
        {
            return !left.Equals(right);
        }
    }
}

