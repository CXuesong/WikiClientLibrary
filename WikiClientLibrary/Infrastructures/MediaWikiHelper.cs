using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// Helper methods for extending MW API.
    /// </summary>
    public static class MediaWikiHelper
    {
        /// <summary>
        /// Create an new instance of <see cref="JsonSerializer"/> for parsing MediaWiki API response.
        /// </summary>
        public static JsonSerializer CreateWikiJsonSerializer()
        {
            return Utility.CreateWikiJsonSerializer();
        }
    }
}
