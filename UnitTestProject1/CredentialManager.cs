using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Tests.UnitTestProject1;

internal static partial class CredentialManager
{

    /// <summary>
    /// The API EntryPoint used for performing page moving/deletion and file uploads.
    /// </summary>
    public static string? DirtyTestsEntryPointUrl { get; private set; }

    /// <summary>
    /// The API EntryPoint used for performing private wiki API tests.
    /// </summary>
    /// <remarks>
    /// A private wiki is a wiki with the following settings
    /// <code>
    /// $wgGroupPermissions['*']['read'] = false;
    /// $wgGroupPermissions['*']['edit'] = false;
    /// $wgGroupPermissions['*']['createaccount'] = false;
    /// </code>
    /// </remarks>
    public static string? PrivateWikiTestsEntryPointUrl { get; private set; }

    /// <summary>
    /// When implemented in your own credential file,
    /// set this property to a function that can login into specific site.
    /// You can use <see cref="WikiSite.ApiEndpoint"/> to determine which site to login into.
    /// </summary>
    private static Func<WikiSite, Task> LoginCoreAsyncHandler { get; set; } = _ =>
        throw new NotSupportedException(
            "To enable login feature, you should set `LoginCoreAsyncHandler` in `Initialize` private function. See http://github.com/cxuesong/WikiClientLibrary for more information.");

    /// <summary>
    /// When implemented in your own credential file,
    /// set this property to a function that can return a <see cref="WikiSite"/> instance
    /// using <see cref="WikiSite(IWikiClient,SiteOptions,string,string)"/> overload
    /// to login to the site during initialization.
    /// You can use <see cref="SiteOptions.ApiEndpoint"/> to determine which site to login into.
    /// </summary>
    private static Func<IWikiClient, SiteOptions, Task<WikiSite>> EarlyLoginCoreAsyncHandler { get; set; } = (_, __) =>
        throw new NotSupportedException(
            "To enable login feature, you should set `EarlyLoginCoreAsyncHandler` in `Initialize` private function. See http://github.com/cxuesong/WikiClientLibrary for more information.");

    /// <summary>
    /// Initialize confidential information.
    /// </summary>
    /// <remarks>You can initialize <see cref="DirtyTestsEntryPointUrl"/> in this method.</remarks>
    static partial void Initialize();

    /// <summary>
    /// Use predefined credential routine, login to the specified site.
    /// </summary>
    public static async Task LoginAsync(WikiSite site)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        await LoginCoreAsyncHandler(site);
        if (!site.AccountInfo.IsUser)
            throw new NotSupportedException("Failed to login into: " + site + " . Check your LoginCoreAsyncHandler implementation.");
    }

    /// <summary>
    /// Use predefined credential routine, return a logged-in WikiSite instance.
    /// </summary>
    public static async Task<WikiSite> EarlyLoginAsync(IWikiClient wikiClient, SiteOptions options)
    {
        if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
        if (options == null) throw new ArgumentNullException(nameof(options));
        var site = await EarlyLoginCoreAsyncHandler(wikiClient, options);
        if (!site.Initialization.IsCompleted)
            throw new InvalidOperationException(
                "You forgot to await WikiSite.Initialization in your EarlyLoginCoreAsyncHandler implementation.");
        if (site == null)
            throw new NotSupportedException("Your EarlyLoginCoreAsyncHandler implementation returned null for site: " +
                                            options.ApiEndpoint + ".");
        if (!site.AccountInfo.IsUser)
            throw new NotSupportedException("Failed to login into: " + site + " . Check your EarlyLoginCoreAsyncHandler implementation.");
        return site;
    }

    static CredentialManager()
    {
        Initialize();
    }

}
