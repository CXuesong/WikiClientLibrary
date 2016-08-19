using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;

namespace UnitTestProject1
{
    internal static partial class CredentialManager
    {
        /// <summary>
        /// When implemented in your own credential file,
        /// login into specific site.
        /// You can use <see cref="WikiClient.EndPointUrl"/> 
        /// to determine which site to login into.
        /// </summary>
        static partial void LoginCore(Site site);

        /// <summary>
        /// Use predefined credential routine, login to the specified site.
        /// </summary>
        public static void Login(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));

        }
    }
}
