using System.Net.Http.Headers;
using System.Reflection;

namespace WikiClientLibrary.Client;

/// <summary>
/// Provides a set of opinionated but also useful methods for <see cref="WikiClient"/> instances.
/// </summary>
/// <remarks>
/// While you can still do well if you just implement your own equivalencies to this class,
/// you are encouraged to open an issue on GitHub if there is any problems or feature requests.
/// </remarks>
public static class WikiClientHelper
{

    /// <inheritdoc cref="BuildUserAgent(Assembly,string)" />
    public static string BuildUserAgent(Assembly assembly)
    {
        return BuildUserAgent(assembly, null);
    }

    /// <summary>
    /// Builds a valid <c>User-Agent</c> header value from the information inferred from the specified assembly,
    /// which can be used in <see cref="WikiClient.ClientUserAgent" />.
    /// </summary>
    /// <param name="assembly">The assembly from which to acquire the user agent information.</param>
    /// <param name="comment">Optional comment in User Agent. Can be <c>null</c>.</param>
    /// <returns>A <c>User-Agent</c> header value string, in the form <c>assembly-name/assembly-version (comment)</c>, such as <c>App1/1.0</c>.</returns>
    /// <remarks>To generate <c>User-Agent</c> for your application automatically, use <c>BuildUserAgent(typeof(Program), "comment")</c>,
    /// where <c>Program</c> is a class in your application assembly.</remarks>
    public static string BuildUserAgent(Assembly assembly, string? comment)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Specified assembly does not have a simple name.");
        return BuildUserAgent(name, assembly.GetName().Version?.ToString(2), comment);
    }

    /// <inheritdoc cref="BuildUserAgent(string,string,string)" />
    public static string BuildUserAgent(string productName)
    {
        return BuildUserAgent(productName, null, null);
    }

    /// <inheritdoc cref="BuildUserAgent(string,string,string)" />
    public static string BuildUserAgent(string productName, string? productVersion)
    {
        return BuildUserAgent(productName, productVersion, null);
    }

    /// <summary>
    /// Builds a valid <c>User-Agent</c> header value that can be used in <see cref="WikiClient.ClientUserAgent" />.
    /// </summary>
    /// <param name="productName">Product name.</param>
    /// <param name="productVersion">Product version, or <c>null</c>.</param>
    /// <param name="comment">Comment, or <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="productName" /> is <c>null</c>.</exception>
    /// <returns>A <c>User-Agent</c> header value string, in the form <c>productName/productVersion (comment)</c>.</returns>
    public static string BuildUserAgent(string productName, string? productVersion, string? comment)
    {
        ArgumentNullException.ThrowIfNull(productName);
        var info1 = new ProductInfoHeaderValue(productName, productVersion);
        if (comment == null) return info1.ToString();

        if (!comment.StartsWith('(')) comment = "(" + comment + ")";
        var info2 = new ProductInfoHeaderValue(comment);
        return info1 + " " + info2;
    }

}
