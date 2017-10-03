using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Wikibase
{
    public struct WikibaseMonolingualText : IEquatable<WikibaseMonolingualText>
    {

        public static readonly WikibaseMonolingualText Empty = new WikibaseMonolingualText();

        public WikibaseMonolingualText(string text, string language)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (language == null) throw new ArgumentNullException(nameof(language));
            // Simple normalization.
            Language = language.Trim().ToLowerInvariant();
        }

        public string Text { get; }

        public string Language { get; }

        /// <inheritdoc />
        public bool Equals(WikibaseMonolingualText other)
        {
            return string.Equals(Text, other.Text) && string.Equals(Language, other.Language);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WikibaseMonolingualText text && Equals(text);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Text != null ? Text.GetHashCode() : 0) * 397) ^ (Language != null ? Language.GetHashCode() : 0);
            }
        }

        public static bool operator ==(WikibaseMonolingualText left, WikibaseMonolingualText right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WikibaseMonolingualText left, WikibaseMonolingualText right)
        {
            return !left.Equals(right);
        }
    }
}
