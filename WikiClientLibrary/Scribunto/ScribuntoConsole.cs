using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Scribunto
{
    /// <summary>
    /// Represents a Scribunto LUA console. (<c>action=scribunto-console</c>)
    /// </summary>
    /// <remarks>
    /// An instance of this class is equivalent to the "Debug console" shown in the Scribunto
    /// Lua module editor page.
    /// </remarks>
    public class ScribuntoConsole
    {

        private const string adhocModuleTitlePrefix = "Module:WCLAdhoc/DummyModule";

        private long? _SessionId;

        /// <summary>
        /// Creates a new Scribunto Lua evaluation console.
        /// </summary>
        /// <param name="site">The MediaWiki site on which to evaluate Lua scripts.</param>
        /// <remarks>
        /// You need to call <see cref="ResetAsync(string,string,CancellationToken)"/> before starting any evaluation operations.
        /// </remarks>
        public ScribuntoConsole(WikiSite site)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
        }

        public WikiSite Site { get; }

        /// <summary>
        /// Gets the currently used full module title.
        /// </summary>
        /// <remarks>
        /// To properly evaluate Lua modules, the title should start with <c>Module:</c> namespace prefix.
        /// </remarks>
        public string ModuleTitle { get; private set; }

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
        /// <remarks>
        /// This operation usually does not change <see cref="SessionId" />, if it's already has a valid value.
        /// To create a new Lua evaluation session with a different <seealso cref="SessionId"/>,
        /// create a new <seealso cref="ScribuntoConsole"/> instance.
        /// </remarks>
        public async Task ResetAsync(string moduleContent, string moduleTitle, CancellationToken cancellationToken)
        {
            if (moduleTitle == null)
                moduleTitle = adhocModuleTitlePrefix;
            ModuleTitle = moduleTitle;
            ScribuntoEvaluationResult result = null;
            try
            {
                result = await InvokeApiAsync(Site, _SessionId, moduleTitle, moduleContent, "=_VERSION", true, cancellationToken);
                if (string.IsNullOrEmpty(result.ReturnValue))
                    throw new UnexpectedDataException("Lua _VERSION value is empty.");
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

        public async Task<ScribuntoEvaluationResult> EvaluateAsync(string expression, CancellationToken cancellationToken)
        {
            ScribuntoEvaluationResult result = null;
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

        internal static async Task<ScribuntoEvaluationResult> InvokeApiAsync(WikiSite site, long? sessionId, string title, string content, string question, bool clear, CancellationToken ct)
        {
            var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "scribunto-console",
                session = sessionId,
                title = title,
                clear = clear,
                question = question,
                content = content
            }), ct);
            var result = jresult.ToObject<ScribuntoEvaluationResult>(Utility.WikiJsonSerializer);
            switch (result.Type)
            {
                case ScribuntoEvaluationResultType.Normal:
                    return result;
                case ScribuntoEvaluationResultType.Error:
                    throw new ScribuntoConsoleException((string)jresult["messagename"], (string)jresult["message"], result);
                default:
                    throw new UnexpectedDataException($"Unexpected evaluation result type: {(string)jresult["type"]}.");
            }
        }

    }

    public enum ScribuntoEvaluationResultType
    {
        Unknown = 0,
        Normal,
        Error,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ScribuntoEvaluationResult
    {

        [JsonProperty("type")]
        public ScribuntoEvaluationResultType Type { get; set; }

        /// <summary>
        /// The text outputted to the console using <c>mw.log</c>.
        /// </summary>
        [JsonProperty("print")]
        public string Output { get; set; }

        /// <summary>
        /// The return value of the evaluated expression.
        /// </summary>
        [JsonProperty("return")]
        public string ReturnValue { get; set; }

        [JsonProperty("session")]
        public long SessionId { get; set; }

        [JsonProperty("sessionIsNew")]
        public bool IsNewSession { get; set; }

        /// <summary>
        /// Server memory consumption of the current evaluation session.
        /// </summary>
        [JsonProperty("sessionSize")]
        public int SessionSize { get; private set; }

        /// <summary>
        /// Maximum allowed server memory consumption of the current evaluation session.
        /// </summary>
        [JsonProperty("sessionMaxSize")]
        public int SessionMaxSize { get; private set; }

    }

}
