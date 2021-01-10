using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WikiClientLibrary.Cargo;
using WikiClientLibrary.Cargo.Linq;
using WikiClientLibrary.Cargo.Schema;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    public class CargoTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public CargoTests(ITestOutputHelper output) : base(output)
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
            ShallowTrace(result.Select(r => r.ToString(Formatting.None)));
            Assert.Equal(10, result.Count);
            Assert.All(result, r => Assert.Equal(r["Page"], r["Name"]));
            Assert.All(result, r => Assert.True((string)r["RP"] == "" || (int)r["RP"] > 0));
        }

        [Fact]
        public async Task CargoErrorTest()
        {
            var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
            var ex = await Assert.ThrowsAsync<MediaWikiRemoteException>(() => site.ExecuteCargoQueryAsync(new CargoQueryParameters
            {
                Tables = new[] { "Skins" },
                Fields = new[] { "NonExistentField" },
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
                .Take(10);
            // Call .AsAsyncEnumerable to ensure we use async Linq call.
            var records = await q.AsAsyncEnumerable().ToListAsync();
            ShallowTrace(records);
            Assert.All(records, r => Assert.Equal(r.SkinName, r.Page));
        }

        [Fact]
        public async Task LinqToCargoDateTimeTest1()
        {
            var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
            var cqContext = new LolCargoQueryContext(site);
            var q = cqContext.Skins
                .OrderBy(s => s.ReleaseDate)
                .Select(s => new { SkinName = s.Name, s.Champion, s.ReleaseDate, s.ReleaseDate.Value.Year })
                .Where(s => s.Year >= 2019 && s.Year <= 2020)
                .Take(100);
            // Call .AsAsyncEnumerable to ensure we use async Linq call.
            var records = await q.AsAsyncEnumerable().ToListAsync();
            ShallowTrace(records);
            Assert.Equal(100, records.Count);
            Assert.All(records, r => Assert.Equal(r.ReleaseDate?.Year, r.Year));
            Assert.All(records, r => Assert.InRange(r.Year, 2019, 2020));
        }

        private class LolCargoQueryContext : CargoQueryContext
        {

            /// <inheritdoc />
            public LolCargoQueryContext(WikiSite wikiSite) : base(wikiSite)
            {
            }

            public ICargoRecordSet<LolSkin> Skins => Table<LolSkin>();

        }

        [Table("Skins")]
        private class LolSkin
        {

            [Column(CargoSpecialColumnNames.PageName)]
            public string Page { get; set; }

            public string Name { get; set; }

            public string Champion { get; set; }

            public int RP { get; set; }

            public DateTime? ReleaseDate { get; set; }

            public ICollection<string> Artists { get; set; }

            public bool IsLegacy { get; set; }

            public string Special { get; set; }

            public bool HasChromas { get; set; }

            public bool IsClassic { get; set; }

            public bool IsReleased { get; set; }

        }

    }

}
