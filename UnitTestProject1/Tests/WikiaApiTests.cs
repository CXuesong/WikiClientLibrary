using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia;
using WikiClientLibrary.Wikia.Discussions;
using WikiClientLibrary.Wikia.WikiaApi;
using Xunit;
using Xunit.Abstractions;

namespace WikiClientLibrary.Tests.UnitTestProject1.Tests
{

    // Wikia doesn't allow us to login on CI environment.
    // Wikia API has been revamped a lot. Wait for some time until to gets more stable.
    [CISkipped]
    public class WikiaApiTests : WikiSiteTestsBase
    {
        /// <inheritdoc />
        public WikiaApiTests(ITestOutputHelper output) : base(output)
        {
            SiteNeedsLogin(Endpoints.WikiaTest);
        }

        [Fact]
        public async Task FetchUsersTest()
        {
            var site = await WikiaTestSiteAsync;
            var users = await site.FetchUsersAsync(new[] { "Jasonr", "angela", "user_not_exist" }).ToArrayAsync();
            ShallowTrace(users);
            Assert.Equal(2, users.Length);
            Assert.Equal("Jasonr", users[0].Name);
            Assert.Equal("Jasonr", users[0].Title);
            Assert.Equal("https://mediawiki119.wikia.org/wiki/User:Jasonr", users[0].UserPageUrl);
            Assert.Equal(1, users[0].Id);
            Assert.Equal("Angela", users[1].Name);
            Assert.Equal("Angela", users[1].Title);
            Assert.Equal("https://mediawiki119.wikia.org/wiki/User:Angela", users[1].UserPageUrl);
            Assert.Equal(2, users[1].Id);
            var user = await site.FetchUserAsync("__mattisManzel_");
            Assert.Equal("MattisManzel", user.Name);
            Assert.Equal("MattisManzel", user.Title);
            Assert.Equal("https://mediawiki119.wikia.org/wiki/User:MattisManzel", user.UserPageUrl);
            Assert.Equal(4, user.Id);
            user = await site.FetchUserAsync("user_not_exist");
            Assert.Null(user);
        }

        [Fact]
        public async Task FetchSiteVariablesTest()
        {
            var site = await WikiaTestSiteAsync;
            var data = site.WikiVariables;
            ShallowTrace(data);
            Assert.Equal(1362703, data.Id);
            Assert.Equal("Dman Wikia | Fandom", data.SiteName);
            Assert.Equal("https://dman.fandom.com", data.BasePath);
            Assert.Equal("/wiki/", data.ArticlePath);
            Assert.Equal(new[] { 0 }, data.ContentNamespaceIds);
            Assert.Equal("en", data.LanguageInfo.ContentLanguage);
            Assert.Equal("ltr", data.LanguageInfo.ContentFlowDirection);
        }

        [Fact]
        public async Task FetchRelatedPagesTest()
        {
            var site = await WikiaTestSiteAsync;
            var mainPage = new WikiPage(site, site.SiteInfo.MainPage);
            await mainPage.RefreshAsync();
            var relatedPages = await site.FetchRelatedPagesAsync(mainPage.Id);
            ShallowTrace(relatedPages);
            // These are just random titles.
            Assert.All(relatedPages, p => Assert.Matches(@"\w+\d+", p.Title));
        }

        [Fact]
        public async Task SearchListTest()
        {
            var site = await WikiaTestSiteAsync;
            var list = new LocalWikiSearchList(site, "test keyword")
            {
                PaginationSize = 10,
            };
            var results = await list.EnumItemsAsync().Take(30).ToListAsync();
            ShallowTrace(results);
            Assert.NotEmpty(results);
            Assert.All(results, p => Assert.True(p.Quality >= list.MinimumArticleQuality));
            var exactMatches = results.Count(p =>
                p.Snippet.Contains("test", StringComparison.OrdinalIgnoreCase)
                || p.Snippet.Contains("keyword", StringComparison.OrdinalIgnoreCase)
            );
            Output.WriteLine($"Exact matches: {exactMatches}/{results.Count}");
            // At least 80% of the items are exact match.
            Assert.True(exactMatches > (int)0.8 * results.Count);
        }

        [SkippableFact]
        public async Task ArticleEnumCommentsTest()
        {
            var site = await WikiaTestSiteAsync;
            var commentArea = new Board(site, "1WEPN1UE18M6N");
            await commentArea.RefreshAsync();
            Skip.IfNot(commentArea.Exists, $"Page [[{commentArea}]] is gone. There is nothing we can do for it.");
            // [[w:c:mediawiki119:1WEPN1UE18M6N]]
            var comments = await commentArea.EnumPostsAsync().ToListAsync();
            ShallowTrace(comments);
            Assert.True(comments.Count >= 90);
            var exactComments = comments.Take(10).Select(p => new Post(site, p.OwnerPage, p.Id)).ToList();
            await exactComments.RefreshAsync(PostQueryOptions.ExactAuthoringInformation);
            Assert.All(comments.Zip(exactComments, (original, exact) => (original: original, exact: exact)),
                pair =>
                {
                    // Assume the author hasn't changed the user name.
                    // No, the assumption doesn't hold,
                    // User:QATestsUser was originally User:WikiaUser
                    //Assert.Equal(pair.original.Author.Name, pair.exact.Author.Name);
                    // The revision time stamp might be some time lagged behind the time stamp in the page titie
                    //Assert.Equal(pair.original.TimeStamp, pair.exact.TimeStamp);
                    // Thus, allow for 15 sec. difference.
                    Assert.True(pair.exact.TimeStamp - pair.original.TimeStamp < TimeSpan.FromSeconds(20),
                        $"Expect TimeStamp = {pair.original.TimeStamp}; Actual TimeStamp = {pair.exact.TimeStamp}.");
                });
        }

        [SkippableFact]
        public async Task ArticlePostCommentTest()
        {
            var site = await WikiaTestSiteAsync;
            var commentArea = new Board(site, "Project:Sandbox");
            await commentArea.RefreshAsync();
            Skip.IfNot(commentArea.Exists, $"Page [[{commentArea}]] is gone. There is nothing we can do for it.");
            var post = await commentArea.NewPostAsync("Test [[comment]].");
            await post.RefreshAsync();
            Assert.Equal("Test [[comment]].", post.Content);
            var rep = await post.ReplyAsync("Test reply.");
            await rep.ReplyAsync("Test reply, 3rd level.");
        }

        [SkippableFact]
        public async Task MessageWallEnumCommentsTest()
        {
            var site = await WikiaTestSiteAsync;
            var commentArea = new Board(site, "Message Wall:QATestsUser");
            await commentArea.RefreshAsync();
            Skip.IfNot(commentArea.Exists, $"Page [[{commentArea}]] is gone. There is nothing we can do for it.");
            var comments = await commentArea.EnumPostsAsync().Take(100).ToListAsync();
            ShallowTrace(comments);
            // There are a lot of comments there...
            Assert.Equal(100, comments.Count);
        }

        [SkippableFact]
        public async Task MessageWallPostCommentTest()
        {
            var site = await WikiaTestSiteAsync;
            var commentArea = new Board(site, site.AccountInfo.Name, WikiaNamespaces.MessageWall);
            await commentArea.RefreshAsync();
            Skip.IfNot(commentArea.Exists, $"Page [[{commentArea}]] is gone. There is nothing we can do for it.");
            var post = await commentArea.NewPostAsync("Test title", "Test [[comment]].", new[] { "Sandbox", "Project:Sandbox" });
            await post.RefreshAsync();
            Assert.Equal("Test [[comment]].<ac_metadata title=\"Test title\" related_topics=\"Sandbox|Project:Sandbox\"> </ac_metadata>", post.Content);
            var rep = await post.ReplyAsync("Test reply.");
            await rep.ReplyAsync("Test reply, 3rd level.");
        }

        [SkippableFact]
        public async Task BoardEnumCommentsTest()
        {
            var site = await WikiaTestSiteAsync;
            var commentArea = new Board(site, "Board:ForumBoard1509020771711");
            await commentArea.RefreshAsync();
            Skip.IfNot(commentArea.Exists, $"Page [[{commentArea}]] is gone. There is nothing we can do for it.");
            var comments = await commentArea.EnumPostsAsync().Take(100).ToListAsync();
            ShallowTrace(comments);
            Assert.True(comments.Count >= 2);
        }

        [SkippableFact]
        public async Task ForumPostCommentTest()
        {
            var site = await WikiaTestSiteAsync;
            var board = new Board(site, "Board:ForumBoard1509020691220");
            await board.RefreshAsync();
            Skip.IfNot(board.Exists, $"Page [[{board}]] is gone. There is nothing we can do for it.");
            var post = await board.NewPostAsync("Comment title here", "Test comment.", new[] { "Sandbox", "Project:Sandbox" });
            var rep = await post.ReplyAsync("Test reply.");
            await rep.ReplyAsync("Test reply, 3rd level.");
        }

    }
}
