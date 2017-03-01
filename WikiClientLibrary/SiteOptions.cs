using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    /// <summary>
    /// Client options for creating a <see cref="Site"/> instance.
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
        /// and WikiClientLibrary is deciding wheter a page is a disambiguation page.
        /// The default disambiguation template {{Disambig}} is always included in the
        /// list, implicitly.</para>
        /// <para>If this value is <c>null</c>, WikiClientLibrary will try to
        /// infer the disambiguation template from [[MediaWiki:Disambiguationspage]].</para>
        /// </remarks>
        public IList<string> DisambiguationTemplates { get; set; }

        /// <summary>
        /// Sets the URL of MedaiWiki API endpoint.
        /// </summary>
        public string ApiEndpoint { get; set; }

        /// <summary>
        /// Whether to disable the refresh of site info and user info
        /// until <see cref="Site.RefreshSiteInfoAsync"/> and <see cref="Site.RefreshUserInfoAsync"/>
        /// are called explicitly.
        /// </summary>
        /// <remarks>
        /// <para>This property affects the initialization of site info (<see cref="Site.SiteInfo"/>,
        /// <see cref="Site.Extensions"/>, <see cref="Site.InterwikiMap"/>,
        /// and <see cref="Site.Namespaces"/>), as well as <see cref="Site.UserInfo"/>.
        /// If the value is <c>true</c>, these info will not be initialized
        /// when calling <see cref="Site.CreateAsync(WikiClient,SiteOptions)"/>, and by the
        /// invocation of <see cref="Site.LogoutAsync"/>, user info will just be invalidated,
        /// with no further internal invocation of <see cref="Site.RefreshUserInfoAsync"/>.</para>
        /// <para>For the priviate wiki where anonymous users cannot access query API,
        /// it's recommended that this property be set to <c>true</c>.
        /// You can first check whether you have already logged in,
        /// and call <see cref="Site.LoginAsync(string,string)"/> If necessary.</para>
        /// <para>The site info and user info should always be initialized before most of the MediaWiki
        /// operations. Otherwise an <see cref="InvalidOperationException"/> will be thrown when
        /// attempting to perform those operations.</para>
        /// <para>In order to decide whether you have already logged in into a private wiki, you can
        /// <list type="number">
        /// <item><description>Call <see cref="Site.CreateAsync(WikiClient,SiteOptions)"/>, with <see cref="ExplicitInfoRefresh"/> set to <c>true</c>.</description></item>
        /// <item><description>Call and <c>await</c> for <see cref="Site.RefreshUserInfoAsync"/>. (Do not use <see cref="Site.RefreshSiteInfoAsync"/>. See the explanation below.)</description></item>
        /// <item><description>If an <see cref="UnauthorizedOperationException"/> is raised, then you should call <see cref="Site.LoginAsync(string,string)"/> to login.</description></item>
        /// <item><description>Otherwise, since you've called <see cref="Site.RefreshUserInfoAsync"/>, you can directly check <see cref="UserInfo.IsUser"/>.
        /// Usually it would be <c>true</c>, since you've already logged in during a previous session. Otherwise, which is a rare case, you may also need to login.</description></item>
        /// </list>
        /// Note that <see cref="Site.RefreshUserInfoAsync"/> will be refreshed automatically after a sucessful
        /// login operation, so you only have to call <see cref="Site.RefreshSiteInfoAsync"/> afterwards.
        /// Nonetheless, both the user info and the site info should be initially refreshed before
        /// you can perform other opertations.
        /// </para>
        /// </remarks>
        public bool ExplicitInfoRefresh { get; set; }

        /// <summary>
        /// Gets/sets the account rights assertion behavior when performing the requests.
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
        /// <param name="apiEndpoint">The URL of MedaiWiki API endpoint.</param>
        public SiteOptions(string apiEndpoint)
        {
            ApiEndpoint = apiEndpoint;
        }

        internal SiteOptions Clone()
        {
            var newInst = (SiteOptions) this.MemberwiseClone();
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
        /// Asserts that your account is logged in per request, if <see cref="Site.UserInfo"/>
        /// indicates that you should have logged in. If the assertion failed,
        /// an <see cref="AccountAssertionFailureException"/> will be thrown.
        /// </summary>
        AssertUser = 1,
        /// <summary>
        /// Checks that your account has the "bot" user right per request, if <see cref="Site.UserInfo"/>
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
    /// Provides a way for client code to automatic re-login, and continue the
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
        /// <summary>The implementation should call <see cref="Site.LoginAsync(string,string)"/> or one of its overloads.</summary>
        Task<bool> Login(Site site);
    }
}