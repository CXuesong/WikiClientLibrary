using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides methods for setting up logger for a class.
    /// </summary>
    public interface IWikiClientLoggable
    {

        /// <summary>
        /// Sets/replaces the logger factory of the specified class.
        /// </summary>
        /// <remarks>
        /// By default the logger factory is passed from <see cref="WikiClient"/> to <see cref="WikiSite"/>,
        /// and from WikiSite to <see cref="WikiPage"/>. To disable the logging for the latter classes, you
        /// may call this method on those classes, passing <c>null</c> as parameter.
        /// </remarks>
        ILoggerFactory LoggerFactory { get; set; }

    }
}
