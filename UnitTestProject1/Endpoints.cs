namespace WikiClientLibrary.Tests.UnitTestProject1;

public static class Endpoints
{

    public const string WikipediaTest2 = "https://test2.wikipedia.org/w/api.php";

    /// <summary>
    /// WMF beta test site. We only apply the tests that cannot be performed in test2.wikipedia.org (e.g. Flow boards).
    /// </summary>
    public const string WikipediaBetaEn = "https://en.wikipedia.beta.wmflabs.org/w/api.php";

    /// <summary>
    /// This is NOT a test site so do not make modifications to the site.
    /// </summary>
    public const string WikipediaEn = "https://en.wikipedia.org/w/api.php";

    /// <summary>
    /// This is NOT a test site so do not make modifications to the site.
    /// </summary>
    public const string WikipediaZh = "https://zh.wikipedia.org/w/api.php";

    /// <summary>
    /// This is NOT a test site so do not make modifications to the site.
    /// </summary>
    public const string WikipediaLzh = "https://zh-classical.wikipedia.org/w/api.php";

    public const string WikimediaCommonsBeta = "https://commons.wikimedia.beta.wmflabs.org/w/api.php";

    public const string Wikidata = "https://www.wikidata.org/w/api.php";

    public const string WikidataTest = "https://test.wikidata.org/w/api.php";

    public const string WikidataBeta = "https://wikidata.beta.wmflabs.org/w/api.php";

    // It's on MW 1.43 now. Well.
    public const string WikiaTest = "https://sandbox.fandom.com/api.php";

    // It's on MW 1.19 as of now.
    public const string TFWiki = "https://tfwiki.net/api.php";

    public const string RuWarriorsWiki = "https://warriors-cats.fandom.com/ru/api.php";

    public const string LolEsportsWiki = "https://lol.fandom.com/api.php";

}
