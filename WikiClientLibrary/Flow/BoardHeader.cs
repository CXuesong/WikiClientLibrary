using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Contains the board header section information for a <see cref="Board"/>.
    /// </summary>
    public class BoardHeader
    {
        public BoardHeader(Site site, string boardTitle)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            Site = site;
            BoardTitle = WikiLink.NormalizeWikiLink(site, boardTitle);
        }

        public Site Site { get; set; }

        public string BoardTitle { get; private set; }

        public string Format { get; private set; }

        /// <summary>
        /// The latest content of the Board header.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Fetches board header information from server.
        /// </summary>
        public Task RefreshAsync()
        {
            return RefreshAsync(CancellationToken.None);
        }

        /// <summary>
        /// Fetches board header information from server.
        /// </summary>
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            // Known Issue: view-header doesn't support multiple page names.
            // Known Issue: the response contains an awful lot of information that cannot be suppressed by setting certain request parameters.
            var jresult = await Site.PostValuesAsync(new
            {
                action = "flow",
                submodule = "view-header",
                page = BoardTitle,
                vhformat = "wikitext"
            }, cancellationToken);
            var jheader = jresult["flow"]["view-header"]["result"]["header"];
            var jcontent = jheader["revision"]["content"];
            if (jcontent == null)
            {
                Content = null;
                Format = null;
            }
            else
            {
                Content = (string) jcontent["content"];
                Format = (string) jcontent["format"];
            }
        }
    }
}
