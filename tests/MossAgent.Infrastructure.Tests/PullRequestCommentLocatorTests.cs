using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestCommentLocatorTests
{
    private const string Patch = "@@ -10,3 +20,3 @@\n common\n-old\n+new\n tail";
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static PullRequestFile File => new("src/sample.cs", "old/sample.cs", "renamed", 1, 1, Patch, PullRequestPatchState.Available);
    private static PullRequestComment Comment => new(11, "inline", "reviewer", "plain text", "src/sample.cs", 21, "RIGHT", new string('b', 40));

    [Theory]
    [InlineData("LEFT", 10, true)]
    [InlineData("LEFT", 11, true)]
    [InlineData("LEFT", 12, true)]
    [InlineData("RIGHT", 20, true)]
    [InlineData("RIGHT", 21, true)]
    [InlineData("RIGHT", 22, true)]
    [InlineData("LEFT", 21, false)]
    [InlineData("RIGHT", 11, false)]
    [InlineData("RIGHT", 23, false)]
    public void ChangedAndContextLinesUseTheirExactSide(string side, int line, bool current)
    {
        var state = PullRequestCommentLocator.Locate(Revision, File, Comment with { Side = side, Line = line });
        Assert.Equal(current ? PullRequestCommentLocationState.Current : PullRequestCommentLocationState.InvalidLine, state);
    }

    [Theory]
    [InlineData(PullRequestPatchState.Missing)]
    [InlineData(PullRequestPatchState.Incomplete)]
    [InlineData(PullRequestPatchState.TooLarge)]
    public void AVisibleLineInAnUnavailablePatchIsNotEnough(PullRequestPatchState state) =>
        Assert.Equal(PullRequestCommentLocationState.PatchUnavailable, PullRequestCommentLocator.Locate(Revision, File with { PatchState = state }, Comment));

    [Fact]
    public void CommitPathAndActualPatchShapeCannotBeSubstituted()
    {
        Assert.Equal(PullRequestCommentLocationState.Historical, PullRequestCommentLocator.Locate(Revision, File, Comment with { CommitRevision = new string('c', 40) }));
        Assert.Equal(PullRequestCommentLocationState.Current, PullRequestCommentLocator.Locate(Revision, File, Comment with { CommitRevision = new string('B', 40) }));
        foreach (var path in new[] { "SRC/sample.cs", "old/sample.cs", "../src/sample.cs" })
            Assert.Equal(PullRequestCommentLocationState.FileNotLoaded, PullRequestCommentLocator.Locate(Revision, File, Comment with { Path = path }));
        Assert.Equal(PullRequestCommentLocationState.FileNotLoaded, PullRequestCommentLocator.Locate(Revision, null, Comment));
        Assert.Equal(PullRequestCommentLocationState.PatchUnavailable, PullRequestCommentLocator.Locate(Revision, File with { Additions = 3 }, Comment));
        Assert.Equal(PullRequestCommentLocationState.PatchUnavailable, PullRequestCommentLocator.Locate(Revision, File with { Patch = Patch[..Patch.LastIndexOf('\n')] }, Comment));
    }

    [Theory]
    [InlineData(null, "RIGHT")]
    [InlineData(0, "RIGHT")]
    [InlineData(-1, "RIGHT")]
    [InlineData(21, null)]
    [InlineData(21, "right")]
    public void OutdatedFileLevelAndMissingSidesCannotProduceAnchors(int? line, string? side) =>
        Assert.Equal(PullRequestCommentLocationState.MissingLine, PullRequestCommentLocator.Locate(Revision, File, Comment with { Line = line, Side = side }));

    [Theory]
    [InlineData(20, "RIGHT", PullRequestCommentLocationState.Current)]
    [InlineData(21, "RIGHT", PullRequestCommentLocationState.Current)]
    [InlineData(19, "RIGHT", PullRequestCommentLocationState.InvalidLine)]
    [InlineData(22, "RIGHT", PullRequestCommentLocationState.InvalidLine)]
    [InlineData(20, "LEFT", PullRequestCommentLocationState.UnsupportedRange)]
    [InlineData(20, null, PullRequestCommentLocationState.UnsupportedRange)]
    public void MultiLineCommentsRequireTheWholeSameSideRange(int start, string? side, PullRequestCommentLocationState expected) =>
        Assert.Equal(expected, PullRequestCommentLocator.Locate(Revision, File, Comment with { StartLine = start, StartSide = side }));

    [Fact]
    public void MultiLineRangesCannotJumpOverMissingHunkContext()
    {
        var file = File with { Patch = "@@ -1 +1 @@\n-old\n+new\n@@ -10 +10 @@\n-old\n+new", Additions = 2, Deletions = 2 };
        Assert.Equal(PullRequestCommentLocationState.InvalidLine, PullRequestCommentLocator.Locate(Revision, file,
            Comment with { Line = 10, StartLine = 1, StartSide = "RIGHT" }));
        Assert.False(PullRequestPatchMap.Parse(Patch, 1, 1)!.ContainsRange("RIGHT", 1, int.MaxValue));
    }
}
