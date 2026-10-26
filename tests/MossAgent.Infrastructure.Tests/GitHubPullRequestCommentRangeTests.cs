using System.Net;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestCommentRangeTests
{
    [Theory]
    [InlineData(1, "RIGHT", true)]
    [InlineData(1, "LEFT", true)]
    [InlineData(null, null, true)]
    [InlineData(0, "RIGHT", false)]
    [InlineData(1, "OTHER", false)]
    public async Task RangeMetadataIsPreservedOrRejectedWithoutFollowingUrls(int? start, string? side, bool valid)
    {
        var account = new SourceAccount(Guid.NewGuid(), "fixture", new Uri("https://api.github.com/"), "synthetic-comment-key", null, true).WithRevision(1);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, 1, "fixture", "repo", false);
        var revision = new PullRequestRevision(7, new string('a', 40), new string('b', 40));
        var probe = new SourceAccountHttpProbe { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent($"[{PullRequestTestData.Comment(start: start, startSide: side)}]") }) };
        var client = new GitHubPullRequestInspectionClient(probe);
        if (valid)
        {
            var comment = Assert.Single((await client.CommentsAsync(account, scope, revision, "inline", 1, TestContext.Current.CancellationToken)).Items);
            Assert.Equal(start, comment.StartLine); Assert.Equal(side, comment.StartSide);
        }
        else Assert.Equal("source_invalid_response", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CommentsAsync(account, scope, revision, "inline", 1, TestContext.Current.CancellationToken))).ErrorCode);
        Assert.Equal(1, probe.DisposedHandlers);
    }
}
