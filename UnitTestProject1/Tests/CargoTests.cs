using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WikiClientLibrary.Cargo;
using WikiClientLibrary.Cargo.Linq;
using WikiClientLibrary.Cargo.Linq.ExpressionVisitors;
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
            }));
            Assert.Equal("MWException", ex.ErrorClass);
        }

        [Fact]
        public async Task LinqToCargoTest()
        {
            var site = await GetWikiSiteAsync(Endpoints.LolEsportsWiki);
            var cqContext = new CargoQueryContext(site);
            var closureParams = new { Champion = "Diana" };
            var q = cqContext.Table<LolSkin>("Skins")
                .Select(s => new { s.Name, s.Champion, s.ReleaseDate })
                .Where(s => s.Champion == closureParams.Champion);
            var q1 = new CargoQueryExpressionTreeReducer().VisitAndConvert(q.Expression, nameof(LinqToCargoTest));
            var q2 = new CargoQueryParametersBuilder().VisitAndConvert(q1, nameof(LinqToCargoTest));
        }

        private class LolSkin
        {

            public string Page { get; set; }

            public string Name { get; set; }

            public string Champion { get; set; }

            public int RP { get; set; }

            public DateTime ReleaseDate { get; set; }

            public ICollection<string> Artists { get; set; }

            public bool IsLegacy { get; set; }

            public string Special { get; set; }

            public bool HasChromas { get; set; }

            public bool IsClassic { get; set; }

            public bool IsReleased { get; set; }

        }

    }

}
