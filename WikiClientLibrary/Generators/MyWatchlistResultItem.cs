using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Generators;

public sealed class MyWatchlistResultItem
{

    public WikiPageStub Page { get; }
    public bool IsChanged { get; }
    public DateTime? ChangedTime { get; }

    public MyWatchlistResultItem(WikiPageStub page, bool isChanged, DateTime? changedTime = null)
    {
        Page = page;
        IsChanged = isChanged;
        ChangedTime = changedTime;
    }

}
