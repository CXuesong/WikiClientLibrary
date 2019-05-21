using System.Runtime.CompilerServices;

namespace WikiClientLibrary.Wikia
{
    /// <summary>
    /// The root namespace for
    /// <a href="https://community.fandom.com/wiki/Community_Central">FANDOM</a>
    /// and
    /// <a href="https://www.wikia.org/">Wikia.org</a>
    /// site-specific support.
    /// </summary>
    /// <remarks>
    /// <para>Wikia uses a modified MediaWiki fork based on MediaWiki v1.19.
    /// Since then, various public or non-publicized Wikia-specific web APIs were developed,
    /// including user-management, chatting, commenting and discussions.</para>
    /// <para>For now, the available API endpoints includes</para>
    /// <list type="bullet">
    /// <item>
    /// <term>Wikia API v1</term>
    /// <description>This is the only publicized API. See its documentation at <a href="https://dev.fandom.com/api/v1">https://dev.fandom.com/api/v1</a>.</description>
    /// </item>
    /// <item>
    /// <term>Nirvana API</term>
    /// <description>This API uses <c>http://{prefix}.wikia.com/wikia.php</c> as endpoint URL.</description>
    /// </item>
    /// <item>
    /// <term>AJAX API</term>
    /// <description>This API uses <c>http://{prefix}.wikia.com/index.php?action=ajax</c> as endpoint URL.</description>
    /// </item>
    /// </list>
    /// <para>For more information on these Wikia-specific API endpoints,
    /// see <a href="https://dev.fandom.com/wiki/Nirvana">wikia:dev:Nirvana</a>.
    /// </para>
    /// </remarks>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Wikia.Discussions
{
    /// <summary>
    /// Contains classes for retrieving and creating comments on Wikia Message Wall and forums.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}
