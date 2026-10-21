using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class MoveHandleBoundaryTests
{
    [Theory]
    [InlineData("source")]
    [InlineData("destination")]
    public async Task ParentReplacedAfterPathValidationCannotMoveOutside(string side)
    {
        MoveHandleFixture? fixture = null;
        var parent = "";
        var sessions = new CallbackPathMoveSessions { BeforePrepare = () =>
        {
            parent = side == "source" ? fixture!.SourceParent : fixture!.TargetParent;
            Directory.Move(parent, parent + "-saved");
            TestDirectoryJunction.CreateAsync(parent, fixture.Outside, fixture.Token).GetAwaiter().GetResult();
        } };
        using var owned = new MoveHandleFixture(sessions);
        fixture = owned;
        var sentinel = Path.Combine(fixture.Outside, side == "source" ? "file.txt" : "moved.txt");
        await File.WriteAllTextAsync(sentinel, "outside sentinel", fixture.Token);
        try
        {
            var result = await fixture.MoveAsync(overwrite: true);
            Assert.False(result.IsSuccess);
            Assert.Equal(side == "source" ? "file_move_unsafe" : "path_denied", result.ErrorCode);
            Assert.Equal("outside sentinel", await File.ReadAllTextAsync(sentinel, fixture.Token));
            Assert.True(File.Exists(side == "source" ? Path.Combine(parent + "-saved", "file.txt") : fixture.Source));
        }
        finally { if (parent.Length > 0 && new DirectoryInfo(parent).LinkTarget is not null) Directory.Delete(parent); }
    }

    [Theory]
    [InlineData("self")]
    [InlineData("nested")]
    [InlineData("source-root")]
    [InlineData("target-root")]
    [InlineData("traversal")]
    public async Task InvalidBoundaryIsRefusedBeforeNativeAcquisition(string scenario)
    {
        using var fixture = new MoveHandleFixture();
        var source = scenario is "self" or "nested" ? fixture.SourceParent : scenario == "source-root" ? fixture.Authorized : fixture.Source;
        var target = scenario switch
        {
            "self" => source,
            "nested" => Path.Combine(source, "new"),
            "target-root" => fixture.Authorized,
            "traversal" => Path.Combine(fixture.Authorized, "..", "outside", "moved.txt"),
            _ => fixture.Target
        };
        var result = await fixture.MoveAsync(source, target);
        Assert.Equal(scenario is "self" or "nested" ? "move_self_denied" : "path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.Sessions.Creates);
        Assert.True(File.Exists(fixture.Source));
    }

    [Fact]
    public async Task MovePreservesSourceAdsButRefusesDiscardingDestinationAds()
    {
        using var fixture = new MoveHandleFixture();
        await File.WriteAllTextAsync(fixture.Source + ":extra", "source metadata", fixture.Token);
        await File.WriteAllTextAsync(fixture.Target, "old target", fixture.Token);
        await File.WriteAllTextAsync(fixture.Target + ":extra", "target metadata", fixture.Token);
        var refused = await fixture.MoveAsync(overwrite: true);
        Assert.Equal("file_move_unsafe", refused.ErrorCode);
        Assert.Equal("target metadata", await File.ReadAllTextAsync(fixture.Target + ":extra", fixture.Token));
        File.Delete(fixture.Target);
        // External deletion does not discard this task's observed target version.
        var moved = await fixture.MoveAsync(context: new(Guid.NewGuid(), fixture.Authorized,
            [fixture.Authorized], MossAgent.Domain.ApprovalPolicy.FullAccess));
        Assert.True(moved.IsSuccess, moved.Summary);
        Assert.Equal("source metadata", await File.ReadAllTextAsync(fixture.Target + ":extra", fixture.Token));
    }

    [Fact]
    public async Task NonEmptyDirectoryTargetIsNeverOverwritten()
    {
        using var fixture = new MoveHandleFixture();
        Directory.CreateDirectory(fixture.Target);
        var sentinel = Path.Combine(fixture.Target, "sentinel");
        await File.WriteAllTextAsync(sentinel, "retained", fixture.Token);
        var result = await fixture.MoveAsync(fixture.SourceParent, overwrite: true);
        Assert.Equal("file_changed", result.ErrorCode);
        Assert.True(File.Exists(fixture.Source));
        Assert.Equal("retained", await File.ReadAllTextAsync(sentinel, fixture.Token));
    }
}
