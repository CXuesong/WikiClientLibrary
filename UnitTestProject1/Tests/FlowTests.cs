using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Flow;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1.Tests
{
    /// <summary>
    /// FlowTests 的摘要说明
    /// </summary>
    public class FlowTests : WikiSiteTestsBase
    {

        /// <inheritdoc />
        public FlowTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task BoardTest()
        {
            var board = new Board(await WpBetaSiteAsync, "Talk:Flow QA");
            await board.RefreshAsync();
            ShallowTrace(board);
            var x = await board.EnumTopicsAsync(20).Take(10).ToArray();
            var y = await board.EnumTopicsAsync(10).ToArray();
            var topics = await board.EnumTopicsAsync(10).Take(10).ToArray();
            ShallowTrace(topics, 3);
            Assert.DoesNotContain(null, topics);
        }

    }
}
