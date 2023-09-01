using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Generators
{
    public class PagesWithPropResultItem
    {
        public WikiPageStub Page { get; }
        public string Value { get; set; }

        public PagesWithPropResultItem(WikiPageStub wikiPageStub, string value)
        {
            Page = wikiPageStub;
            Value = value;
        }
    }
}