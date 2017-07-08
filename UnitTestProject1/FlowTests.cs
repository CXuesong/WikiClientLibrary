using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary;
using WikiClientLibrary.Flow;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using static UnitTestProject1.Utility;

namespace UnitTestProject1
{
    /// <summary>
    /// FlowTests 的摘要说明
    /// </summary>
    [TestClass]
    public class FlowTests
    {
        private static readonly Lazy<Site> _WpBetaSite = new Lazy<Site>(() => CreateWikiSite(EntryPointWikipediaBetaEn));

        public static Site WpBetaSite => _WpBetaSite.Value;

        [TestMethod]
        public void BoardTest()
        {
            var board = new Board(WpBetaSite, "Talk:Flow QA");
            AwaitSync(Task.WhenAll(board.RefreshAsync(), board.Header.RefreshAsync()));
            ShallowTrace(board);
            Assert.AreEqual(ContentModels.FlowBoard, board.ContentModel);
            var topics = AwaitSync(board.EnumTopicsAsync(10).Take(10).ToArray());
            ShallowTrace(topics);
        }
    }
}
