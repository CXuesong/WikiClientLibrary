using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Flow;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using Xunit;
using Xunit.Abstractions;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
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
            await Task.WhenAll(board.RefreshAsync(), board.Header.RefreshAsync());
            ShallowTrace(board);
            Assert.Equal(ContentModels.FlowBoard, board.ContentModel);
            var topics = await board.EnumTopicsAsync(10).Take(10).ToArray();
            ShallowTrace(topics);
        }

    }
}
