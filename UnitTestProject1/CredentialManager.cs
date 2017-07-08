using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace UnitTestProject1
{
    internal static partial class CredentialManager
    {
        /// <summary>
        /// The API EntryPoint used for performing page moving/deletion and file uploads.
        /// </summary>
        public static string DirtyTestsEntryPointUrl { get; private set; }

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
        public static string PrivateWikiTestsEntryPointUrl { get; private set; }

        /// <summary>
        /// When implemented in your own credential file,
        /// login into specific site.
        /// You can use <see cref="WikiClient.EndPointUrl"/> 
        /// to determine which site to login into.
        /// </summary>
        /// <exception cref="NotSupportedException">Logging into the site specified is not supported yet.</exception>
        static partial void LoginCore(Site site);

        /// <summary>
        /// Initialize confidential information.
        /// </summary>
        /// <remarks>You can initialize <see cref="DirtyTestsEntryPointUrl"/> in this method.</remarks>
        static partial void Initialize();

        /// <summary>
        /// Invoked by the implementation of <see cref="LoginCore"/>,
        /// logins into specified site synchronously.
        /// </summary>
        private static void Login(Site site, string userName, string password)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Utility.AwaitSync(site.LoginAsync(userName, password));
        }

        /// <summary>
        /// Use predefined credential routine, login to the specified site.
        /// </summary>
        public static void Login(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            LoginCore(site);
            if (!site.AccountInfo.IsUser)
                // This is usually because LoginCore hasn't been implemented yet.
                throw new NotSupportedException("To enable login feature, you should implement LoginCore private function. See http://github.com/cxuesong/WikiClientLibrary for more information.");
        }

        public static void Logout(Site site)
        {
            Utility.AwaitSync(site.LogoutAsync());
        }

        static CredentialManager()
        {
            Initialize();
        }
    }
}
