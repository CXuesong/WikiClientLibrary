using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WikiClientLibrary.Pages;

/// <summary>
/// Provides a set of opinionated but also useful methods for MediaWiki pages and page titles.
/// </summary>
/// <remarks>
/// While you can still do well if you just implement your own equivalencies to this class,
/// you are encouraged to open an issue on GitHub if there is any problems or feature requests.
/// </remarks>
public static class PageHelper
{

    private static readonly Regex dabTitlePartMatcher = new(".+?(?=[\\s_]*[\\(（])", RegexOptions.Compiled);

    /// <summary>
    /// Strips disambiguation part of the title from the full article title.
    /// </summary>
    /// <param name="originalTitle">The original full article title.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalTitle" /> is <c>null</c>.</exception>
    /// <returns>The article title without disambiguation part, or the original title given if no such part can be found.</returns>
    /// <remarks>
    /// <para>According to the current naming practice of disambiguation on Wikipedia,
    /// the page title with disambiguation part is like: <c>Politics (Aristotle)</c>.
    /// Note there is a space before the left bracket. However, for the sake of compatibility,
    /// especially on the Wiki sites outside Wikimedia Projects, the method also handles the following cases
    /// <list type="bullet">
    /// <item><term>Politics(Aristotle)</term><description>Missing, or extra whitespaces before the bracket.</description></item>
    /// <item><term>政治学 （亚里士多德）</term><description>Full-width left bracket instead of half-width one.</description></item>
    /// </list>
    /// </para>
    /// <para>For general concept on disambiguation, see <a href="https://en.wikipedia.org/wiki/Wikipedia:Disambiguation">Wikipedia:Disambiguation</a>.</para>
    /// </remarks>
    public static string StripTitleDisambiguation(string originalTitle)
    {
        ArgumentNullException.ThrowIfNull(originalTitle);
        var match = dabTitlePartMatcher.Match(originalTitle);
        return match.Success ? match.Value : originalTitle;
    }

    /// <summary>
    /// Sanitizes the MediaWiki page content locally for SHA1 evaluation.
    /// </summary>
    /// <param name="content">The content to be sanitized.</param>
    /// <remarks>
    /// <para>This method normalizes line breaks into <c>"\n"</c>, and trims the trailing white spaces.
    /// Leading white spaces are kept to align with the MediaWiki behavior.</para>
    /// <para>Due to the limitation of offline page content normalization, this function will not expand
    /// and <c>~~~~</c> or <c>{{subst:}}</c> magic. This could still cause different content and thus SHA1
    /// hash from the MediaWiki server being evaluated, possibly causing false-positive when checking
    /// whether the page content retrieved from MediaWiki server is the same as the local version.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <c>null</c>.</exception>
    public static string SanitizePageContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content == "" || !char.IsWhiteSpace(content[^1]) && !content.Contains('\r'))
            return content;

        var stringBuilder = new StringBuilder(content);
        stringBuilder.Replace("\r\n", "\n");
        stringBuilder.Replace('\r', '\n');
        var length = stringBuilder.Length;
        while (length > 0 && char.IsWhiteSpace(stringBuilder[length - 1]))
            length--;
        return stringBuilder.ToString(0, length);
    }

    /// <summary>
    /// Evaluates SHA1 hash of the specified text content, in UTF-8 encoding.
    /// </summary>
    /// <param name="content">The text content to be hashed.</param>
    /// <returns>The lower-case hexadecimal hash string.</returns>
    /// <remarks>To evaluate the SHA1 hash of sanitized text content (especially when
    /// you are checking revision hash locally before sending remote requests), use
    /// <see cref="EvaluateSanitizedSha1" />.</remarks>
    public static string EvaluateSha1(string content)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(content));
        var stringBuilder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            stringBuilder.Append(b.ToString("x2"));
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Evaluates SHA1 hash of the sanitized version of specified text content, in UTF-8 encoding.
    /// </summary>
    /// <param name="content">The text content to be hashed.</param>
    /// <returns>The lower-case hexadecimal hash string.</returns>
    /// <remarks>
    /// <para>You may compare the returned hash directly to <see cref="Revision.Sha1" />
    /// to determine whether certain remote revision is much likely to have the same
    /// content as specified by <paramref name="content" />.</para>
    /// <para>Due to the limitation of offline normalization, the SHA1 evaluated locally
    /// could still be different from the server-side value. See the "Remarks" section of
    /// <seealso cref="SanitizePageContent"/> for more information.</para>
    /// </remarks>
    /// <seealso cref="EvaluateSha1" />
    public static string EvaluateSanitizedSha1(string content)
    {
        var s = SanitizePageContent(content);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        var stringBuilder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            stringBuilder.AppendFormat(b.ToString("x2"));
        return stringBuilder.ToString();
    }

}
