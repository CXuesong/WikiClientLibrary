using System;
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
            SiteNeedsLogin(Endpoints.WikipediaBetaEn);
        }

        [Fact]
        public async Task BoardTest()
        {
            var board = new Board(await WpBetaSiteAsync, "Talk:Flow QA");
            await board.RefreshAsync();
            ShallowTrace(board);
            var topics = await board.EnumTopicsAsync(10).Take(10).ToArray();
            ShallowTrace(topics, 3);
            Assert.DoesNotContain(null, topics);
        }

        [Fact]
        public async Task BoardReplyTest()
        {
            var board = new Board(await WpBetaSiteAsync, "Talk:Flow QA");
            var topic1 = await board.NewTopicAsync("Test ''topic''", $"This is the content of '''test topic'''.\n\n{DateTime.UtcNow:R}");
            var post1 = await topic1.ReplyAsync("How's the weather today?");
            var post2 = await topic1.ReplyAsync("Reply to the topic.");
            var post11 = await post1.ReplyAsync("It's sunny.");
            var post21 = await post2.ReplyAsync("Reply to the topic, again.");
            // Refetch the topic and check.
            var topic = await board.EnumTopicsAsync(1).First();
            ShallowTrace(topic);
            Assert.Equal(topic1.Title, topic.Title);
            Assert.Equal("Test ''topic''", topic.TopicTitleRevision.Content);
            Assert.Equal(3, topic.Posts.Count);     // Including the "This is the content of..." post.
            Assert.Contains("This is the content", topic.Posts[0].LastRevision.Content);
            Assert.Equal(1, topic.Posts[1].Replies.Count);
            Assert.Equal(1, topic.Posts[2].Replies.Count);
            // Refresh the post and check.
            await post11.RefreshAsync();
            Assert.Equal("It's sunny.", post11.LastRevision.Content);
        }

    }
}
