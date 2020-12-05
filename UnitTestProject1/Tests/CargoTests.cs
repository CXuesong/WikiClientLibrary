using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WikiClientLibrary.Cargo;
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

    }

}
