namespace WikiClientLibrary.Sites;

/// <summary>
/// Client options for creating a <see cref="WikiSite"/> instance.
/// </summary>
public class SiteOptions
{

    /// <summary>
    /// The name of default disambiguation template.
    /// </summary>
    /// <remarks>
    /// The default disambiguation template {{Disambig}} is always included in the
    /// list, implicitly.
    /// </remarks>
    public const string DefaultDisambiguationTemplate = "Template:Disambig";

    /// <summary>
    /// Specifies a list of disambiguation templates explicitly.
    /// </summary>
    /// <remarks>
    /// <para>This list is used when there's no Disambiguator on the MediaWiki site,
    /// and WikiClientLibrary is deciding whether a page is a disambiguation page.
    /// The default disambiguation template {{Disambig}} is always included in the
    /// list, implicitly.</para>
    /// <para>If this value is <c>null</c>, WikiClientLibrary will try to
    /// infer the disambiguation template from [[MediaWiki:Disambiguationspage]].</para>
    /// </remarks>
    public IList<string>? DisambiguationTemplates { get; set; }

    /// <summary>
    /// Sets the URL of MediaWiki API endpoint.
    /// </summary>
    public string ApiEndpoint { get; set; } = "";

    /// <summary>
    /// Gets/sets the account rights assertion behavior when performing the requests. (MW 1.25+)
    /// </summary>
    /// <remarks>Defaults to <see cref="AccountAssertionBehavior.AssertAll"/>.</remarks>
    public AccountAssertionBehavior AccountAssertion { get; set; } = AccountAssertionBehavior.AssertAll;

    /// <summary>
    /// Initializes with empty settings.
    /// </summary>
    public SiteOptions()
    {
    }

    /// <summary>
    /// Initializes with API endpoint URL.
    /// </summary>
    /// <param name="apiEndpoint">The URL of MediaWiki API endpoint.</param>
    public SiteOptions(string apiEndpoint)
    {
        ApiEndpoint = apiEndpoint;
    }

    /// <summary>
    /// Make a deep-clone of current instance.
    /// </summary>
    public virtual SiteOptions Clone()
    {
        var newInst = (SiteOptions)this.MemberwiseClone();
        newInst.DisambiguationTemplates = DisambiguationTemplates?.ToArray();
        return newInst;
    }

}

/// <summary>
/// See https://www.mediawiki.org/wiki/API:Assert .
/// </summary>
[Flags]
public enum AccountAssertionBehavior
{

    /// <summary>
    /// Do not assert for user's login status when performing API requests. Not recommended.
    /// </summary>
    None = 0,

    /// <summary>
    /// Asserts that your account is logged in per request, if <see cref="WikiSite.AccountInfo"/>
    /// indicates that you should have logged in. If the assertion failed,
    /// an <see cref="AccountAssertionFailureException"/> will be thrown.
    /// </summary>
    AssertUser = 1,

    /// <summary>
    /// Checks that your account has the "bot" user right per request, if <see cref="WikiSite.AccountInfo"/>
    /// indicates that you should have logged in as bot. If the assertion failed,
    /// an <see cref="AccountAssertionFailureException"/> will be thrown.
    /// </summary>
    AssertBot = 2,

    /// <summary>
    /// Checks for "bot" user right, or "user" if the former is not applicable per request.
    /// </summary>
    AssertAll = AssertUser | AssertBot

}

/// <summary>
/// Provides a way for client code to automatically re-login, and continue the
/// impeded requests without raising <see cref="AccountAssertionFailureException"/>,
/// when account assertion has failed.
/// </summary>
/// <remarks>See <see cref="SiteOptions.AccountAssertion"/> for more information.</remarks>
public interface IAccountAssertionFailureHandler
{

    /// <summary>
    /// Called when an account assertion has failed.
    /// </summary>
    /// <param name="site">The site where the assertion failed and needs user to login.</param>
    /// <returns>
    /// A task that returns a <c>bool</c>, indicating whether user should have logged in. If this method still cannot make user correctly logged in, it should be <c>false</c>,
    /// in which case, an <see cref="AccountAssertionFailureException"/> will still be thrown to the API invoker.
    /// </returns>
    /// <summary>The implementation should call <see cref="WikiSite.LoginAsync(string,string)"/> or one of its overloads.</summary>
    Task<bool> Login(WikiSite site);

}
