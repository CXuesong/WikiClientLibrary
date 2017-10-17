using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Represents a token placeholder in the <see cref="MediaWikiFormRequestMessage"/>.
    /// This enables <see cref="WikiSite"/> to detect bad tokens.
    /// </summary>
    public sealed class WikiSiteToken
    {

        public static WikiSiteToken Edit = new WikiSiteToken("edit");

        public static WikiSiteToken Move = new WikiSiteToken("move");

        public static WikiSiteToken Delete = new WikiSiteToken("delete");

        public static WikiSiteToken Patrol = new WikiSiteToken("patrol");

        public WikiSiteToken(string type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public string Type { get; }

    }
}
