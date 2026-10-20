using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceReviewContextBuilderTests
{
    [Fact]
    public void ContextIsBoundedAndReportsMissingAndClippedData()
    {
        var scope = new SourceRepositoryScope(Guid.NewGuid(), Guid.NewGuid(), 1, "fixture", "repo");
        var summary = new PullRequestSummary(7, "change", "author", "open", false, new string('x', 10000), new string('a', 40), new string('b', 40));
        var files = Enumerable.Range(1, 20).Select(index => new PullRequestFile($"file-{index}", null, "modified", 1, 1,
            new string('x', 32768), PullRequestPatchState.Available)).ToArray();
        var context = SourceReviewContextBuilder.Build(scope, summary, files, [], []);
        Assert.InRange(context.Length, 1, SourceReviewChatBinding.MaximumContextLength);
        Assert.Contains("Untrusted PR snapshot", context, StringComparison.Ordinal);
        Assert.Contains("Omitted data is not an empty result", context, StringComparison.Ordinal);
        Assert.Contains("snapshot text clipped", context, StringComparison.Ordinal);
        Assert.Contains("[snapshot budget reached]", context, StringComparison.Ordinal);
        Assert.Contains(summary.HeadRevision, context, StringComparison.Ordinal);
    }

    [Fact]
    public void InvestigationUsesOnlyFailedLoadedChecksAndNeverContainsAnUnboundedLog()
    {
        Assert.Null(SourceReviewContextBuilder.Investigation([new(1, "passing", "completed", "success", "runs")]));
        var checks = Enumerable.Range(1, 40).Select(index => new PullRequestCheck(index, new string('中', 1024), "completed", "failure", "runs"));
        var text = SourceReviewContextBuilder.Investigation(checks.ToArray());
        Assert.NotNull(text); Assert.InRange(text.Length, 1, 8192);
        Assert.True(SourceReviewContextBuilder.IsFailure(new(1, "legacy", "error", null, "statuses")));
        Assert.False(SourceReviewContextBuilder.IsFailure(new(1, "pending", "queued", null, "runs")));
    }
}
