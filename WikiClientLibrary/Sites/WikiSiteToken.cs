using System;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Represents a token placeholder in the <see cref="MediaWikiFormRequestMessage"/>.
    /// This enables <see cref="WikiSite"/> to detect bad tokens.
    /// </summary>
    /// <remarks>
    /// For backwards-compatibility, please use the most specific token type where possible
    /// (e.g., <see cref="Edit"/> or <see cref="Move"/> instead of <see cref="Csrf"/>).
    /// </remarks>
    public sealed class WikiSiteToken
    {

        public static WikiSiteToken Edit = new WikiSiteToken("edit");

        public static WikiSiteToken Move = new WikiSiteToken("move");

        public static WikiSiteToken Delete = new WikiSiteToken("delete");

        public static WikiSiteToken Patrol = new WikiSiteToken("patrol");

        /// <summary>General CSRF token. This token type is not supported prior to MW 1.24.</summary>
        public static WikiSiteToken Csrf = new WikiSiteToken("csrf");

        public WikiSiteToken(string type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public string Type { get; }

    }
}
