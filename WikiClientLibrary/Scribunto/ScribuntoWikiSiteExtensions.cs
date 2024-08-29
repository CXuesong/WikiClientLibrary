using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Scribunto;

public static class ScribuntoWikiSiteExtensions
{

    private static readonly JsonSerializerOptions luaTableDefaultSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <inheritdoc cref="ScribuntoLoadDataAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName)
    {
        return ScribuntoLoadDataAsync<T>(site, moduleName, null, null, CancellationToken.None);
    }

    /// <inheritdoc cref="ScribuntoLoadDataAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName, CancellationToken cancellationToken)
    {
        return ScribuntoLoadDataAsync<T>(site, moduleName, null, null, cancellationToken);
    }

    /// <inheritdoc cref="ScribuntoLoadDataAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName, JsonSerializerOptions? serializerOptions)
    {
        return ScribuntoLoadDataAsync<T>(site, moduleName, serializerOptions, CancellationToken.None);
    }

    /// <summary>
    /// Imports the Lua module with the specified name and gets the module content.
    /// </summary>
    /// <returns>The deserialized Lua evaluation result of the module.</returns>
    /// <remarks>
    /// This overload is intended to provide similar behavior to
    /// <a href="https://www.mediawiki.org/wiki/Extension:Scribunto/Lua_reference_manual#mw.loadData"><c>mw.loadData</c></a>,
    /// it works as long as the imported module meets the requirement of <c>loadData</c> function.
    /// </remarks>
    /// <inheritdoc cref="ScribuntoLoadDataAsync{T}(WikiSite,string,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName,
        JsonSerializerOptions? serializerOptions, CancellationToken cancellationToken)
    {
        return ScribuntoLoadDataAsync<T>(site, moduleName, null, serializerOptions, cancellationToken);
    }

    /// <inheritdoc cref="ScribuntoLoadDataAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName, string? epilog)
    {
        return ScribuntoLoadDataAsync<T>(site, moduleName, epilog, null, CancellationToken.None);
    }

    /// <inheritdoc cref="ScribuntoLoadDataAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName, string? epilog,
        CancellationToken cancellationToken)
    {
        return ScribuntoLoadDataAsync<T>(site, moduleName, epilog, null, cancellationToken);
    }

    /// <summary>
    /// Imports the Lua module with the specified name and evaluates the specified Lua code with it.
    /// </summary>
    /// <typeparam name="T">The expected evaluation return value type.</typeparam>
    /// <param name="site">The MediaWiki site on which to evaluate the module.</param>
    /// <param name="moduleName">Name of the module to be imported, with or without <c>Module:</c> prefix.</param>
    /// <param name="epilog">The Lua code snippet used to return value from the imported module (denoted as <c>p</c> in Lua),
    /// or <c>null</c> to use default epilog (<c>return p</c>).</param>
    /// <param name="serializerOptions">The JsonSerializerOptions used to deserialize the return value from JSON, or <c>null</c> to use default JSON serializer.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The deserialized Lua evaluation result.</returns>
    public static Task<T> ScribuntoLoadDataAsync<T>(this WikiSite site, string moduleName, string? epilog,
        JsonSerializerOptions? serializerOptions, CancellationToken cancellationToken)
    {
        if (site == null)
            throw new ArgumentNullException(nameof(site));
        if (string.IsNullOrEmpty(moduleName))
            throw new ArgumentException(Prompts.ExceptionArgumentNullOrEmpty, nameof(moduleName));
        cancellationToken.ThrowIfCancellationRequested();
        var moduleLink = WikiLink.Parse(site, moduleName);
        var normalizedModuleName = moduleLink.FullTitle;
        if (string.IsNullOrEmpty(moduleLink.NamespaceName))
            normalizedModuleName = "Module:" + normalizedModuleName;
        if (epilog == null)
            epilog = "return p";
        var sb = new StringBuilder("-- ScribuntoLoadDataAsync\n\n", 64 + normalizedModuleName.Length + epilog.Length);
        sb.Append("local p = require([==[");
        sb.Append(normalizedModuleName);
        sb.Append("]==])\n\n");
        sb.Append(epilog);
        sb.AppendLine();
        return ScribuntoExecuteLuaAsync<T>(site, sb.ToString(), serializerOptions, cancellationToken);
    }

    /// <inheritdoc cref="ScribuntoExecuteLuaAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoExecuteLuaAsync<T>(this WikiSite site, string moduleContent)
    {
        return ScribuntoExecuteLuaAsync<T>(site, moduleContent, null, CancellationToken.None);
    }

    /// <inheritdoc cref="ScribuntoExecuteLuaAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoExecuteLuaAsync<T>(this WikiSite site, string moduleContent, CancellationToken cancellationToken)
    {
        return ScribuntoExecuteLuaAsync<T>(site, moduleContent, null, cancellationToken);
    }

    /// <inheritdoc cref="ScribuntoExecuteLuaAsync{T}(WikiSite,string,JsonSerializerOptions,CancellationToken)"/>
    public static Task<T> ScribuntoExecuteLuaAsync<T>(this WikiSite site, string moduleContent, JsonSerializerOptions serializerOptions)
    {
        return ScribuntoExecuteLuaAsync<T>(site, moduleContent, serializerOptions, CancellationToken.None);
    }

    /// <summary>
    /// Evaluates an ad-hoc Lua module with the specified module content in Scribunto Lua console, and gets the returned value.
    /// </summary>
    /// <typeparam name="T">The expected evaluation return value type.</typeparam>
    /// <param name="site">The MediaWiki site on which to evaluate the module.</param>
    /// <param name="moduleContent">The module content to be evaluated. You need to use <c>return</c> statement to return any value from the module.</param>
    /// <param name="serializerOptions">The JsonSerializerOptions used to deserialize the return value from JSON, or <c>null</c> to use default JSON serializer.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The deserialized Lua evaluation result.</returns>
    /// <remarks>
    /// <para>This method will let MediaWiki server to evaluate <paramref name="moduleContent"/> as an ad-hoc Lua module,
    /// and to serialize the return value into JSON with
    /// <a href="https://www.mediawiki.org/wiki/Extension:Scribunto/Lua_reference_manual#mw.text.jsonEncode"><c>mw.text.jsonEncode</c></a>
    /// method. The returned value will be deserialized by WCL with your specified <paramref name="serializerOptions"/>.
    /// You need to read the documentation for <c>jsonEncode</c> carefully, as there might be some common pitfalls, such as
    /// empty Lua table will be serialized as JSON <c>[]</c> rather than <c>{}</c>.</para>
    /// <para>Due to the nature of JSON serialization and deserialization,
    /// you cannot return Lua functions or tables containing functions in the module.</para>
    /// </remarks>
    public static async Task<T> ScribuntoExecuteLuaAsync<T>(this WikiSite site, string moduleContent,
        JsonSerializerOptions? serializerOptions, CancellationToken cancellationToken)
    {
        if (site == null)
            throw new ArgumentNullException(nameof(site));
        if (string.IsNullOrEmpty(moduleContent))
            throw new ArgumentException(Prompts.ExceptionArgumentNullOrEmpty, nameof(moduleContent));
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ScribuntoConsole.InvokeApiAsync(site, null, ScribuntoConsole.AdhocModuleTitlePrefix,
            moduleContent, "=mw.text.jsonEncode(p)", true, cancellationToken);
        if (string.IsNullOrEmpty(result.ReturnValue))
            throw new UnexpectedDataException(Prompts.ExceptionScribuntoConsoleReturnEmpty);

        return JsonSerializer.Deserialize<T>(result.ReturnValue, serializerOptions ?? luaTableDefaultSerializerOptions)!;
    }

}
