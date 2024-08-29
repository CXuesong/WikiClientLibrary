using System.ComponentModel.DataAnnotations.Schema;
using WikiClientLibrary.Cargo;
using WikiClientLibrary.Cargo.Linq;
using WikiClientLibrary.Cargo.Schema;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Tests.UnitTestProject1.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests;

public class CargoTests : WikiSiteTestsBase, IClassFixture<WikiSiteProvider>
{

    /// <inheritdoc />
    public CargoTests(ITestOutputHelper output, WikiSiteProvider wikiSiteProvider) : base(output, wikiSiteProvider)
    {
    }

    [Fact]
    public async Task BasicCargoQueryTest()
    {
        var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
        var result = await site.ExecuteCargoQueryAsync(new CargoQueryParameters
        {
            Tables = new[] { "Skins" },
            Fields = new[] { "_pageName=Page", "Name", "RP" },
            Where = "DATEDIFF(ReleaseDate, {d'2010-1-1'}) < 0",
            Limit = 10,
        });
        ShallowTrace(result.Select(r => r.ToJsonString()));
        Assert.Equal(10, result.Count);
        Assert.All(result, r => Assert.Equal((string?)r["Page"], (string?)r["Name"]));
        Assert.All(result, r => Assert.True(r["RP"] == null || Convert.ToInt32((string?)r["RP"]) > 0));
    }

    [Fact]
    public async Task CargoErrorTest()
    {
        var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
        var ex = await Assert.ThrowsAsync<MediaWikiRemoteException>(() => site.ExecuteCargoQueryAsync(new CargoQueryParameters
        {
            Tables = new[] { "Skins" }, Fields = new[] { "NonExistentField" },
        }));
        Assert.Equal("MWException", ex.ErrorClass);
    }

    [Fact]
    public async Task LinqToCargoTest1()
    {
        var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
        var cqContext = new LolCargoQueryContext(site);
        var closureParams = new { Champion = "Diana" };
        var q = cqContext.Skins
            .OrderBy(s => s.RP)
            .ThenByDescending(s => s.ReleaseDate)
            .Select(s => new { s.Page, SkinName = s.Name, s.Champion, s.ReleaseDate })
            .Where(s => s.Champion == closureParams.Champion && s.ReleaseDate < new DateTime(2020, 1, 1))
            .Take(100);
        // Call .AsAsyncEnumerable to ensure we use async Linq call.
        var records = await q.AsAsyncEnumerable().ToListAsync();
        ShallowTrace(records);
        Assert.All(records, r => Assert.Equal(r.SkinName, r.Page));
    }

    /// <summary>Tests YEAR function.</summary>
    [Fact]
    public async Task LinqToCargoDateTimeTest1()
    {
        var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
        var cqContext = new LolCargoQueryContext(site) { PaginationSize = 50 };
        var q = cqContext.Skins
            .OrderBy(s => s.ReleaseDate)
            .Select(s => new { SkinName = s.Name, s.Champion, s.ReleaseDate, s.ReleaseDate!.Value.Year })
            .Where(s => s.Year >= 2019 && s.Year <= 2020)
            .Take(100);
        // Call .AsAsyncEnumerable to ensure we use async Linq call.
        var records = await q.AsAsyncEnumerable().ToListAsync();
        ShallowTrace(records);
        Assert.Equal(99, records.Count);
        Assert.All(records, r => Assert.Equal(r.ReleaseDate?.Year, r.Year));
        Assert.All(records, r => Assert.InRange(r.Year, 2019, 2020));
    }

    /// <summary>Tests DATE_DIFF and DATE_SUB functions.</summary>
    [Theory]
    [InlineData(365)]
    [InlineData(730)]
    [InlineData(500.123)]
    public async Task LinqToCargoDateTimeTest2(double backtrackDays)
    {
        var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
        var cqContext = new LolCargoQueryContext(site);
        var q = cqContext.Skins
            .OrderBy(s => s.ReleaseDate)
            .Where(s => CargoFunctions.DateDiff(s.ReleaseDate, DateTime.Now - TimeSpan.FromDays(backtrackDays)) >= 0)
            .Take(10);
        // Call .AsAsyncEnumerable to ensure we use async Linq call.
        var records = await q.AsAsyncEnumerable().ToListAsync();
        ShallowTrace(records, 3);
        // Left some buffer as server time may deviate from the client time.
        var expectedMinReleaseDate = DateTime.UtcNow - TimeSpan.FromDays(backtrackDays + 1);
        Output.WriteLine("expectedMinReleaseDate = {0:O}", expectedMinReleaseDate);
        Assert.All(records, r => Assert.True(r.ReleaseDate == null || r.ReleaseDate > expectedMinReleaseDate));
    }

    /// <summary>Tests HOLDS and HOLDS_LIKE function.</summary>
    [Fact]
    public async Task LinqToCargoDateTimeTest3()
    {
        var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
        var cqContext = new LolCargoQueryContext(site);
        // Cargo build on lolwiki does not support `&&` very well for now.
        var q = from s in cqContext.Skins
            where CargoFunctions.Holds(s.Artists, "David Villegas") || CargoFunctions.HoldsLike(s.Artists, "% Studio")
            where s.ReleaseDate >= new DateTime(2020, 1, 1) && s.ReleaseDate <= new DateTime(2021, 1, 1)
            orderby s.ReleaseDate descending
            // HACK projection Artists = s.Artists works here as by default we use (,) as delimiter.
            // TODO pass through CargoCollectionAttribute to ProjectionExpression.
            select new { s.Page, s.Name, s.Artists, s.ReleaseDate };
        var records = await q.Take(50).AsAsyncEnumerable().ToListAsync();
        ShallowTrace(records, 3);
        Assert.All(records, r =>
            Assert.Contains(r.Artists, a => a == "David Villegas" || a.EndsWith(" Studio"))
        );
    }


    private class LolCargoQueryContext : CargoQueryContext
    {

        /// <inheritdoc />
        public LolCargoQueryContext(WikiSite wikiSite) : base(wikiSite)
        {
            this.PaginationSize = 20;
        }

        public ICargoRecordSet<LolSkin> Skins => Table<LolSkin>();

    }

    [Table("Skins")]
    private class LolSkin
    {

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public LolSkin()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

        [Column(CargoSpecialColumnNames.PageName)]
        public string Page { get; set; }

        public string Name { get; set; }

        public string Champion { get; set; }

        public int? RP { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public ICollection<string> Artists { get; set; }

        public bool IsLegacy { get; set; }

        public string Special { get; set; }

        public bool HasChromas { get; set; }

        public bool IsClassic { get; set; }

        public bool IsReleased { get; set; }

    }

}
