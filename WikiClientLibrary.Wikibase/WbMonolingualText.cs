using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// Represents a text in certain language.
    /// </summary>
    /// <remarks>The language code is normalized into lower-case in this structure.</remarks>
    public struct WbMonolingualText : IEquatable<WbMonolingualText>
    {

        public static readonly WbMonolingualText Null = new WbMonolingualText();

        /// <summary>
        /// Initializes a new <see cref="WbMonolingualText"/> from language code and text.
        /// </summary>
        /// <param name="language">The language code. It will be converted to lower-case.</param>
        /// <param name="text">The text.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="language"/> or <paramref name="text"/> is <c>null</c>.</exception>
        public WbMonolingualText(string language, string text)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            if (text == null) throw new ArgumentNullException(nameof(text));
            // Simple normalization.
            Language = language.Trim().ToLowerInvariant();
            Text = text;
        }

        internal WbMonolingualText(string language, string text, bool bypassPreprocess)
        {
            Debug.Assert(bypassPreprocess);
            Debug.Assert(language != null);
            Debug.Assert(text != null);
            Language = language;
            Text = text;
        }

        /// <summary>The language code.</summary>
        public string Language { get; }

        /// <summary>The text.</summary>
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

