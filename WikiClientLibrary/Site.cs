using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using System.Security;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public class Site
    {
        public WikiClient WikiClient { get; }

        private Dictionary<int, NamespaceInfo> _Namespaces = new Dictionary<int, NamespaceInfo>();

        public static async Task<Site> GetAsync(WikiClient wikiClient)
        {
            var site = new Site(wikiClient);
            await site.RefreshAsync();
            return site;
        }

        protected Site(WikiClient wikiClient)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            WikiClient = wikiClient;
            //Namespaces = new ReadOnlyDictionary<int, NamespaceInfo>(_Namespaces);
        }

        public async Task RefreshAsync()
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "general|namespaces|namespacealiases"
            });
            var qg = (JObject)jobj["query"]["general"];
            var ns = (JObject)jobj["query"]["namespaces"];
            //Name = (string) qg["sitename"];
            SiteInfo = qg.ToObject<SiteInfo>(Utility.WikiJsonSerializer);
            _Namespaces = ns.ToObject<Dictionary<int, NamespaceInfo>>(Utility.WikiJsonSerializer);
            Namespaces = new ReadOnlyDictionary<int, NamespaceInfo>(_Namespaces);
            jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                meta = "userinfo",
                uiprop = "blockinfo|groups|hasmsg|rights"
            });
            UserInfo = ((JObject)jobj["query"]["userinfo"]).ToObject<UserInfo>(Utility.WikiJsonSerializer);
        }

        public SiteInfo SiteInfo { get; private set; }

        public UserInfo UserInfo { get; private set; }

        public IReadOnlyDictionary<int, NamespaceInfo> Namespaces { get; private set; }

        private async Task<JObject> GetTokensAsync(string tokenTypeExpr)
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                meta = "tokens",
                type = tokenTypeExpr,
            });
            var warnings = jobj["warnings"]?["tokens"];
            if (warnings != null)
            {
                // "*": "Unrecognized value for parameter 'type': xxxx"
                if (warnings["*"] != null)
                    throw new OperationFailedException((string) warnings["*"]);
                throw new OperationFailedException(warnings.ToString());
            }
            return (JObject) jobj["query"]["tokens"];
        }

        /// <summary>
        /// Request tokens for operations.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public async Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes)
        {
            if (tokenTypes == null) throw new ArgumentNullException(nameof(tokenTypes));
            IEnumerable<KeyValuePair<string, JToken>> jobj = await GetTokensAsync(string.Join("|", tokenTypes));
            // Remove "token" in the result
            return jobj.ToDictionary(p => p.Key.EndsWith("token")
                ? p.Key.Substring(0, p.Key.Length - 5)
                : p.Key,
                p => (string) p.Value);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public async Task<string> GetTokenAsync(string tokenType)
        {
            if (tokenType == null) throw new ArgumentNullException(nameof(tokenType));
            if (tokenType.Contains("|"))
                throw new ArgumentException("Pipe character in token type name.", nameof(tokenType));
            var dict = await GetTokensAsync(new[] {tokenType});
            return dict.Values.Single();
        }

        public Task LoginAsync(string userName, string password)
        {
            return LoginAsync(userName, password, null);
        }

        public async Task LoginAsync(string userName, string password, string domain)
        {
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            var token = await GetTokenAsync("login");
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "login",
                lgname = userName,
                lgpassword = password,
                lgtoken = token,
                lgdomain = domain,
            });
            var result = (string) jobj["login"]["result"];
            string message = null;
            switch (result)
            {
                case "Success":
                    return;
                case "Aborted":
                    message =
                        "The login using the main account password (rather than a bot password) cannot proceed because user interaction is required. The clientlogin action should be used instead.";
                    break;
                case "NeedToken":       // We should have got correct token.
                case "WrongToken":
                    throw new UnexpectedDataException($"Unexpected login result: {result} .");
            }
            message = (string) jobj["login"]["reason"] ?? message;
            throw new OperationFailedException(result, message);
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(SiteInfo.SiteName) ? WikiClient.EndPointUrl : SiteInfo.SiteName;
        }
    }
}
