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
            var topicTitle = "Test ''topic'' - placeholder";
            var topic1 = await board.NewTopicAsync(topicTitle, $"This is the content of '''test topic'''.\n\n{DateTime.UtcNow:R}");
            var post1 = await topic1.ReplyAsync("How's the weather today?");
            var post2 = await topic1.ReplyAsync("Reply to the topic.");
            var post11 = await post1.ReplyAsync("It's sunny.");
            var post21 = await post2.ReplyAsync("Reply to the topic, again.");

            // Refetch the topic and check.
            var topic = await board.EnumTopicsAsync(1).First();
            ShallowTrace(topic);
            Assert.Equal(topic1.Title, topic.Title);
            Assert.Equal(topicTitle, topic.TopicTitle);
            Assert.Equal(3, topic.Posts.Count); // Including the "This is the content of..." post.
            Assert.Contains("This is the content", topic.Posts[0].Content);
            Assert.Equal(1, topic.Posts[1].Replies.Count);
            Assert.Equal(1, topic.Posts[2].Replies.Count);

            // Refresh the post and check.
            await post11.RefreshAsync();
            Assert.Equal("It's sunny.", post11.Content);
            Assert.False(post11.LastRevision.IsModerated);
            Assert.Equal(ModerationState.None, post11.LastRevision.ModerationState);

            // Edit topic title
            topicTitle = topic.TopicTitle = "Test ''topic'' - " + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            await topic.UpdateTopicTitleAsync();
            await topic.RefreshAsync();
            Assert.Equal(topicTitle, topic.TopicTitle);

            // Moderate a post
            await post1.ModerateAsync(ModerationAction.Hide, "Test to hide the post.");
            await post1.RefreshAsync();
            Assert.True(post1.LastRevision.IsModerated);
            Assert.Equal(ModerationState.Hidden, post1.LastRevision.ModerationState);
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() => post1.ReplyAsync("This attempt will fail."));

            // Moderate a topic
            await topic1.ModerateAsync(ModerationAction.Hide, "Test to hide the topic.");
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() => topic1.ReplyAsync("This attempt will fail."));
            await topic1.ModerateAsync(ModerationAction.Restore, "Test to restore the topic.");

            await topic1.LockAsync("Test discussion closed.");

            var summary = "Case closed - " + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            topic1.Summary = summary;
            await topic1.UpdateSummaryAsync();
            await topic1.RefreshAsync();
            Assert.Equal(summary, topic1.Summary);
        }

    }
}
