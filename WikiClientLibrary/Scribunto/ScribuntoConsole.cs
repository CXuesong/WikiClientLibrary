using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Scribunto;

/// <summary>
/// Represents a Scribunto Lua console. (<c>action=scribunto-console</c>)
/// </summary>
/// <remarks>
/// <para>An instance of this class is equivalent to the "Debug console" shown in the Scribunto
/// Lua module editor page.</para>
/// <para>The MediaWiki site need to have <a href="https://www.mediawiki.org/wiki/Extension:Scribunto">Scribunto extension</a> installed
/// to support this feature.</para>
/// <para>You need to call <see cref="ResetAsync(string,string,CancellationToken)"/> or its overload before
/// starting any evaluation operations.</para>
/// </remarks>
public class ScribuntoConsole
{

    internal const string AdhocModuleTitlePrefix = "Module:WCLAdhoc/DummyModule";

    private long? _SessionId;

    /// <summary>
    /// Creates a new Scribunto Lua evaluation console.
    /// </summary>
    /// <param name="site">The MediaWiki site on which to evaluate Lua scripts.</param>
    /// <remarks>
    /// You need to call <see cref="ResetAsync(string,string,CancellationToken)"/> or its overload
    /// before starting any evaluation operations.
    /// </remarks>
    public ScribuntoConsole(WikiSite site)
    {
        Site = site ?? throw new ArgumentNullException(nameof(site));
    }

    public WikiSite Site { get; }

    /// <summary>
    /// Gets the currently used full module title, including the <c>Module:</c> namespace prefix.
    /// The title does not necessarily exist on the MediaWiki site.
    /// </summary>
    public string? ModuleTitle { get; private set; }

    /// <summary>
    /// Gets the current evaluation session ID.
    /// </summary>
    public long SessionId => _SessionId ?? 0;

    /// <summary>
    /// Gets the server memory consumption of the current evaluation session.
    /// </summary>
    public int SessionSize { get; private set; }

    /// <summary>
    /// Gets the maximum allowed server memory consumption of the current evaluation session.
    /// </summary>
    public int SessionMaxSize { get; private set; }

    /// <summary>
    /// Resets the current Lua evaluation session with the specified module content and module title.
    /// </summary>
    /// <param name="moduleContent">Lua module content. The return value of the expression will be the return value of the module.</param>
    /// <param name="moduleTitle">Title of the Lua module, or <c>null</c> to use a dummy module name. To properly evaluate Lua modules, the title should start with <c>Module:</c> namespace prefix.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ScribuntoConsoleException">There is Scribunto console error evaluating module content.</exception>
    /// <exception cref="UnexpectedDataException">Cannot validate Scribunto console working properly. The sanity test does not pass.</exception>
    /// <exception cref="NotSupportedException">The MediaWiki site does not support Scribunto console.</exception>
    /// <remarks>
    /// <para>Upon reset of the console, this client library will attempt to evaluate <c>=_VERSION</c> and validates
    /// whether server can return any value (even if it's <c>nil</c>). This is the sanity test.</para>
    /// <para>This operation usually does not change <see cref="SessionId" />, if it's already has a valid value.
    /// To create a new Lua evaluation session with a different <seealso cref="SessionId"/>,
    /// create a new <seealso cref="ScribuntoConsole"/> instance.</para>
    /// </remarks>
    public async Task ResetAsync(string? moduleContent, string? moduleTitle, CancellationToken cancellationToken)
    {
        if (moduleTitle == null)
            moduleTitle = AdhocModuleTitlePrefix;
        ModuleTitle = moduleTitle;
        ScribuntoEvaluationResult? result = null;
        try
        {
            result = await InvokeApiAsync(Site, _SessionId, moduleTitle, moduleContent, "=_VERSION", true, cancellationToken);
            if (string.IsNullOrEmpty(result.ReturnValue))
                throw new UnexpectedDataException(Prompts.ExceptionScribuntoResetCannotValidate);
        }
        catch (ScribuntoConsoleException ex)
        {
            result = ex.EvaluationResult;
            throw;
        }
        finally
        {
            if (result != null)
            {
                _SessionId = result.SessionId;
                SessionSize = result.SessionSize;
                SessionMaxSize = result.SessionMaxSize;
            }
        }
    }

    /// <summary>
    /// Resets the current Lua evaluation session with the specified module content and dummy module title.
    /// </summary>
    /// <inheritdoc cref="ResetAsync(string,string,CancellationToken)"/>
    public Task ResetAsync(string moduleContent, CancellationToken cancellationToken)
    {
        return ResetAsync(moduleContent, null, cancellationToken);
    }

    /// <summary>
    /// Resets the current Lua evaluation session with the specified module content and dummy module title.
    /// </summary>
    /// <inheritdoc cref="ResetAsync(string,string,CancellationToken)"/>
    public Task ResetAsync(string moduleContent)
    {
        return ResetAsync(moduleContent, null, CancellationToken.None);
    }

    /// <summary>
    /// Resets the current Lua evaluation session with empty module content and dummy module title.
    /// </summary>
    /// <inheritdoc cref="ResetAsync(string,string,CancellationToken)"/>
    public Task ResetAsync()
    {
        return ResetAsync(null, null, CancellationToken.None);
    }

    /// <inheritdoc cref="EvaluateAsync(string,CancellationToken)"/>
    public Task<ScribuntoEvaluationResult> EvaluateAsync(string expression)
    {
        return EvaluateAsync(expression, CancellationToken.None);
    }

    /// <summary>
    /// Evaluates the specified Lua expression in the console.
    /// </summary>
    /// <param name="expression">The Lua expression to be evaluated.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ScribuntoConsoleException">There is Scribunto console error evaluating module content.</exception>
    /// <returns>The console evaluation result.</returns>
    public async Task<ScribuntoEvaluationResult> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        ScribuntoEvaluationResult? result = null;
        try
        {
            result = await InvokeApiAsync(Site, _SessionId, ModuleTitle, null, expression, false, cancellationToken);
            return result;
        }
        catch (ScribuntoConsoleException ex)
        {
            result = ex.EvaluationResult;
            throw;
        }
        finally
        {
            if (result != null)
            {
                _SessionId = result.SessionId;
                SessionSize = result.SessionSize;
                SessionMaxSize = result.SessionMaxSize;
            }
        }
    }

    internal static async Task<ScribuntoEvaluationResult> InvokeApiAsync(WikiSite site, long? sessionId, string? title, string? content,
        string question, bool clear, CancellationToken ct)
    {
        JsonNode jresult;
        try
        {
            jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "scribunto-console",
                // Since wikimedia/mediawiki-extensions-Scribunto@0f2585244cbdc22580cc431745328a8f1fb270bd (1.40.0-wmf.5)
                token = site.SiteInfo.Version.Above(1, 40, 0, MediaWikiDevChannel.Wmf, 5) ? WikiSiteToken.Csrf : null,
                session = sessionId,
                title = title,
                clear = clear,
                question = question,
                content = content,
            }), ct);
        }
        catch (InvalidActionException ex)
        {
            throw new NotSupportedException(
                "The MediaWiki site does not support Scribunto console. Check whether the required extension has been installed.", ex);
        }
        var result = jresult.Deserialize<ScribuntoEvaluationResult>(MediaWikiHelper.WikiJsonSerializerOptions);
        return result.Type switch
        {
            ScribuntoEvaluationResultType.Normal => result,
            ScribuntoEvaluationResultType.Error => throw new ScribuntoConsoleException((string)jresult["messagename"],
                (string)jresult["message"], result),
            _ => throw new UnexpectedDataException($"Unexpected evaluation result type: {(string)jresult["type"]}."),
        };
    }

}

/// <summary>
/// The type of Scribunto console evaluation result.
/// </summary>
public enum ScribuntoEvaluationResultType
{

    /// <summary>Unknown / invalid evaluation result type.</summary>
    Unknown = 0,

    /// <summary>Normal evaluation result. Evaluation completed successfully.</summary>
    Normal,

    /// <summary>Evaluation error received from server. It will cause <seealso cref="ScribuntoConsoleException"/>.</summary>
    Error,

}

[JsonContract]
public sealed class ScribuntoEvaluationResult
{

    /// <summary>The evaluation result type.</summary>
    [JsonPropertyName("type")]
    public ScribuntoEvaluationResultType Type { get; init; }

    /// <summary>The text outputted to the console using <c>mw.log</c>.</summary>
    /// <seealso cref="ReturnValue"/>
    [JsonPropertyName("print")]
    public string Output { get; init; } = "";

    /// <summary>The string representation of the evaluation return value.</summary>
    /// <remarks>
    /// The returned value will be in its string representation.
    /// For example, Lua <c>nil</c> will be CLR string <c>"nil"</c>;
    /// both Lua number <c>123</c> and string <c>"123"</c> will be CLR string <c>"123"</c>.
    /// </remarks>
    /// <seealso cref="Output"/>
    [JsonPropertyName("return")]
    public string ReturnValue { get; init; } = "";

    /// <summary>The current Scribunto console session ID.</summary>
    [JsonPropertyName("session")]
    public long SessionId { get; init; }

    /// <summary>Whether the server indicates this session ID is new.</summary>
    /// <remarks>
    /// Note that as of MW 1.34, this property is true only
    /// if the session with <seealso cref="SessionId"/> is just created,
    /// or the session has been lost on server (e.g. due to session timeout).
    /// If you resets/clears the session, which does not change the session ID,
    /// this property will be <c>false</c>.
    /// </remarks>
    [JsonPropertyName("sessionIsNew")]
    public bool IsNewSession { get; init; }

    /// <summary>Server memory consumption of the current evaluation session.</summary>
    [JsonPropertyName("sessionSize")]
    public int SessionSize { get; init; }

    /// <summary>Maximum allowed server memory consumption of the current evaluation session.</summary>
    [JsonPropertyName("sessionMaxSize")]
    public int SessionMaxSize { get; init; }

}
