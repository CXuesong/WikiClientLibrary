namespace WikiClientLibrary.Pages.Queries.Properties;

/// <summary>
/// A marker interface which indicates the implementation type is an immutable group
/// of property values associated with <see cref="WikiPage"/> that can be fetched from
/// MediaWiki server.
/// </summary>
/// <remarks>
/// It's recommended that you derive your custom property groups from <see cref="WikiPagePropertyGroup"/>
/// instead of directly implementing <see cref="IWikiPagePropertyGroup"/> interface.
/// </remarks>
public interface IWikiPagePropertyGroup
{

}

/// <inheritdoc />
/// <summary>
/// A base class for an immutable group of extendable <see cref="WikiPage" /> properties.
/// The default implementation for <see cref="IWikiPagePropertyGroup" />.
/// </summary>
public class WikiPagePropertyGroup : IWikiPagePropertyGroup
{

}
