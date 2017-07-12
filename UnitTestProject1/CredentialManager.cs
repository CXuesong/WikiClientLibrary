using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        /// set this property to a function that can login into specific site.
        /// You can use <see cref="Site.ApiEndpoint"/> to determine which site to login into.
        /// </summary>
        private static Func<Site, Task> LoginCoreAsyncHandler { get; set; }

        /// <summary>
        /// Initialize confidential information.
        /// </summary>
        /// <remarks>You can initialize <see cref="DirtyTestsEntryPointUrl"/> in this method.</remarks>
        static partial void Initialize();

        /// <summary>
        /// Use predefined credential routine, login to the specified site.
        /// </summary>
        public static async Task LoginAsync(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (LoginCoreAsyncHandler == null)
            {
                // This is usually because LoginCore hasn't been implemented yet.
                throw new NotSupportedException("To enable login feature, you should implement LoginCoreAsync private function. See http://github.com/cxuesong/WikiClientLibrary for more information.");
            }
            await LoginCoreAsyncHandler(site);
            if (!site.AccountInfo.IsUser)
                throw new NotSupportedException("Failed to login into: " + site + " . Check your LoginCoreAsync implementation.");
        }

        static CredentialManager()
        {
            Initialize();
        }
    }
}
