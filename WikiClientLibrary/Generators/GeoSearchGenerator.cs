using System.Globalization;
using System.Text.Json.Nodes;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Searches for articles around the given point (determined either by coordinates or by article name).
/// (<a href="https://www.mediawiki.org/wiki/Extension:GeoData#prop=coordinates">mw:Extension:GeoData#prop=coordinates</a>)
/// </summary>
/// <seealso cref="GeoCoordinatesPropertyProvider"/>
public class GeoSearchGenerator : WikiPageGenerator<GeoSearchResultItem>
{

    /// <inheritdoc />
    public GeoSearchGenerator(WikiSite site) : base(site)
    {
    }

    /// <summary>
    /// Gets/sets the coordinate around which to search.
    /// </summary>
    /// <remarks>The <see cref="GeoCoordinate.Dimension"/> property will be ignored.</remarks>
    public GeoCoordinate TargetCoordinate { get; set; }

    /// <summary>
    /// Gets/sets the title of page around which to search.
    /// </summary>
    /// <remarks>
    /// When this property is set to a non-null value,
    /// <see cref="TargetCoordinate"/> and <see cref="BoundingRectangle"/> will be ignored.
    /// </remarks>
    public string? TargetTitle { get; set; }

    /// <summary>
    /// Geographical bounding box to search in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specifying a bounding box that is too large can cause <see cref="ArgumentException"/>
    /// when enumerating the items. See <see cref="OnEnumItemsFailed"/>.
    /// </para>
    /// <para>
    /// Specifying this field with a non-empty rectangle will cause
    /// <see cref="TargetCoordinate"/> and <see cref="Radius"/> being ignored.
    /// </para>
    /// </remarks>
    public GeoCoordinateRectangle BoundingRectangle { get; set; }

    /// <summary>
    /// Gets/sets the search radius in meters (10-10000).
    /// </summary>
    /// <remarks>The default value is <c>10</c>, i.e. 10 meters.</remarks>
    public double Radius { get; set; } = 10;

    /// <summary>
    /// Only list pages in these namespaces.
    /// </summary>
    /// <value>Selected ids of namespace, or <c>null</c> to use default settings (i.e. only in main namespace).</value>
    public IEnumerable<int>? NamespaceIds { get; set; }

    /// <summary>
    /// Gets/set a value indicating whether to include secondary coordinates in the search results.
    /// </summary>
    public bool IncludesSecondaryCoordinates { get; set; }

    /// <inheritdoc />
    public override string ListName => "geosearch";

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><see cref="BoundingRectangle"/> is too big.</exception>
    protected override void OnEnumItemsFailed(Exception exception)
    {
        if (exception is OperationFailedException ofe && ofe.ErrorCode == "toobig")
        {
            throw new ArgumentException(ofe.ErrorMessage, nameof(BoundingRectangle), exception);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        var prop = new Dictionary<string, object?>
        {
            { "gsradius", Radius },
            { "gsnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds) },
            { "gsprimary", IncludesSecondaryCoordinates ? "all" : "primary" },
            { "gsglobe", TargetCoordinate.Globe },
            { "gslimit", PaginationSize },
        };
        if (TargetTitle != null)
        {
            // When searching by page title, it would be better for MW API to
            // assume `gsglobe` corresponds to the `globe` of that page,
            // but there is currently no such behavior.
            prop["gspage"] = TargetTitle;
        }
        else if (!BoundingRectangle.IsEmpty)
        {
            var rect = BoundingRectangle;
            // This is way too large than the width acceptable by MW API.
            // And this causes weird `Right` value.
            if (rect.Width > 180)
                throw new ArgumentException("Bounding box is too big.", nameof(BoundingRectangle));
            rect.Normalize();
            var right = rect.Right;
            if (right > 180) right -= 360;
            prop["gsbbox"] = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}",
                rect.Top, rect.Left, rect.Bottom, right);
        }
        else
        {
            prop["gscoord"] = TargetCoordinate.Latitude.ToString(CultureInfo.InvariantCulture)
                              + "|" + TargetCoordinate.Longitude.ToString(CultureInfo.InvariantCulture);
        }
        return prop;
    }

    /// <inheritdoc />
    protected override GeoSearchResultItem ItemFromJson(JsonNode json)
    {
        return new GeoSearchResultItem(MediaWikiHelper.PageStubFromJson(json.AsObject()),
            MediaWikiHelper.GeoCoordinateFromJson(json.AsObject()),
            json["primary"] != null, (double)json["dist"]);
    }

}
