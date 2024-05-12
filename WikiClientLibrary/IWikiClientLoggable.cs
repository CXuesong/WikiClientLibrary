using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WikiClientLibrary;

/// <summary>
/// Provides methods for setting up logger for a class.
/// </summary>
public interface IWikiClientLoggable
{

    /// <summary>
    /// Replaces the logger factory of the specified instance.
    /// </summary>
    /// <remarks>Setting this property to <c>null</c> is equivalent to setting it to <see cref="NullLogger.Instance"/>.</remarks>
    ILogger Logger { get; set; }

}
