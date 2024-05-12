namespace WikiClientLibrary.Pages;

/// <summary>
/// Represents the details of purge failure on a MediaWiki page.
/// </summary>
/// <seealso cref="WikiPage.PurgeAsync()"/>
public class PurgeFailureInfo
{

    internal PurgeFailureInfo(WikiPageStub page, string? invalidReason)
    {
        Page = page;
        InvalidReason = invalidReason;
    }

    public WikiPageStub Page { get; }

    public bool IsMissing => Page.IsMissing;

    public bool IsInvalid => Page.IsInvalid;

    public string? InvalidReason { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        if (InvalidReason != null)
            return Page + ": " + InvalidReason;
        return Page.ToString();
    }
}