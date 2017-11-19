using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary
{
    /// <summary>
    /// The root namespace of Wiki Client Library.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }

}

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// This namespace contains networking infrastructure for accessing MediaWiki and
    /// other wiki-related endpoints.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// This namespace contains networking infrastructure for accessing wiki sites
    /// via MediaWiki endpoint.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// This namespace contains classes to perform operations on MediaWiki pages.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Pages.Queries
{
    /// <summary>
    /// This namespace contains configurable classes that can build parameters for MediaWiki API
    /// requests, which later can be used for querying a sequence of MediaWiki pages
    /// from the server.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Pages.Queries.Properties
{
    /// <summary>
    /// This namespace contains property groups along with their providers
    /// for querying various page properties of MediaWiki pages. 
    /// </summary>
    /// <remarks>
    /// <para>When querying for MediaWiki pages from server, the client can also ask for a subset of properties for the page
    /// of interest. Depending on the installation of extensions, the server supports different set of property modules.
    /// For a list of commonly-used property modules,
    /// see <a href="https://www.mediawiki.org/wiki/API:Properties">mw:API:Properties</a>.</para>
    /// <para>The classes in this namespace deal with property modules that applies to pages only.
    /// For property modules that don't need to be associated with MediaWiki pages
    /// (e.g. <a href="https://www.mediawiki.org/wiki/API:Stashimageinfo">prop=stashimageinfo</a>),
    /// you might need to initiate the request via <see cref="WikiSite.InvokeMediaWikiApiAsync(WikiRequestMessage,CancellationToken)"/>.
    /// For most of the properties that returns a sequence of values
    /// (e.g. <a href="https://www.mediawiki.org/wiki/API:Revisions">prop=revisions</a>,
    /// <a href="https://www.mediawiki.org/wiki/API:Links">prop=links</a>),
    /// please refer to the derived classes of <see cref="WikiPagePropertyList{T}"/>,
    /// which allows you to enumerate the items with automatic list-continuation support.
    /// There is one special case, however, for <c>prop=revisions</c>, that it has been implemented as
    /// <see cref="RevisionsPropertyProvider"/> and <see cref="RevisionsGenerator"/>. For the distinction
    /// between these classes, see their respective documentations.
    /// </para>
    /// </remarks>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Pages.Parsing
{
    /// <summary>
    /// This namespace contains classes for parsing wikitext into HTML on the server-side.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Files
{
    /// <summary>
    /// This namespace contains classes for file management on MediaWiki sites.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}


namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// This namespace contains the implementations for various MediaWiki <c>list</c>s and <c>generator</c>s.
    /// </summary>
    /// <remarks>
    /// <para>MediaWiki lists and generators allows you to enumerate through all the pages or other item sequence,
    /// with pagination support. In Wiki Client Library, these functionalities are implemented using
    /// <see cref="IAsyncEnumerable{T}"/> from <c>Ix.Async</c> package.</para>
    /// <para>For basic concepts on MediaWiki lists and generators, see
    /// <a href="https://www.mediawiki.org/wiki/API:Lists">mw:API:Lists</a> and
    /// <a href="https://www.mediawiki.org/wiki/API:Generator">mw:API:Generator</a>.</para>
    /// <para>The inheritance diagram of generator classes in this namespace is as follows</para>
    /// <para><img src="/images/wcl-generator-classes.png"/></para>
    /// </remarks>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Generators.Primitive
{

    /// <summary>
    /// This namespace contains the primitive classes that allows you to implement your own customized
    /// MediaWiki <c>list</c>s and <c>generator</c>s.
    /// </summary>
    /// <remarks>
    /// <para>MediaWiki lists and generators allows you to enumerate through all the pages or other item sequence,
    /// with pagination support. In Wiki Client Library, these functionalities are implemented using
    /// <see cref="IAsyncEnumerable{T}"/> from <c>Ix.Async</c> package.</para>
    /// <para>For basic concepts on MediaWiki lists and generators, see
    /// <a href="https://www.mediawiki.org/wiki/API:Lists">mw:API:Lists</a> and
    /// <a href="https://www.mediawiki.org/wiki/API:Generator">mw:API:Generator</a>.</para>
    /// </remarks>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// This namespace contains classes that help you to extend Wiki Client Library.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

namespace WikiClientLibrary.Infrastructures.Logging
{
    /// <summary>
    /// This namespace contains classes that help you to
    /// extend Wiki Client Library, especially, by providing
    /// some helper methods on logging.
    /// </summary>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}

