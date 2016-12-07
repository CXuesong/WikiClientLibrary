using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Represents a Flow topic page.
    /// </summary>
    /// <remarks>
    /// <para>The content model of such page should be <c>flow-board</c>.</para>
    /// <para>See https://www.mediawiki.org/wiki/Extension:Flow for more information about Flow extension.</para>
    /// <para>Note that the development of Flow extension seems now paused. See https://en.wikipedia.org/wiki/Wikipedia:Flow for more information.</para>
    /// </remarks>
    public class FlowBoard : Page
    {
        /// <inheritdoc />
        public FlowBoard(Site site, string title) : base(site, title)
        {
        }

        /// <inheritdoc />
        public FlowBoard(Site site, string title, int defaultNamespaceId) : base(site, title, defaultNamespaceId)
        {
        }

        /// <inheritdoc />
        public FlowBoard(Site site) : base(site)
        {
        }
    }
}
