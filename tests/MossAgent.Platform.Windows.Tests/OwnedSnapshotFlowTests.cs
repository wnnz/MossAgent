using System.Text;
using MossAgent.Testing;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class OwnedSnapshotFlowTests
{
    [Fact]
    public async Task SuccessfulEditRetainsTwoHashedSnapshotsReadableThroughNativeBoundary()
    {
        using var fixture = new OwnedArtifactFixture();
        var sessions = new ObservedArtifactSessions();
        var result = await fixture.WriteAsync(sessions);
        Assert.True(result.IsSuccess, result.Summary);
        var change = Assert.Single(result.FileChanges!);
        Assert.True(change.IsValid);
        Assert.Equal(2, sessions.Sessions.Count);
        Assert.All(sessions.Sessions, session => { Assert.True(session.IsRetained); Assert.False(session.Stream.CanRead); });
        var reader = new FileChangeSnapshotReader();
        var bytes = await reader.ReadChangeAsync(change, fixture.Context, fixture.Token);
        Assert.Equal("handle fixture", Encoding.UTF8.GetString(bytes.Before));
        Assert.Equal("updated", Encoding.UTF8.GetString(bytes.After));
        Assert.Equal("updated", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
        Assert.Equal(2, Directory.GetFiles(fixture.ArtifactRoot).Length);
    }

    [Theory]
    [InlineData("conflict", "file_changed")]
    [InlineData("cancelled", "tool_cancelled")]
    [InlineData("deadline", "tool_timeout")]
    [InlineData("revoked", "path_denied")]
    public async Task FailureAfterBothSnapshotsCompleteDeletesBothWithoutCommittingProject(string scenario, string error)
    {
        using var fixture = new OwnedArtifactFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        var roots = new List<string> { fixture.Project };
        var sessions = new ObservedArtifactSessions { Completed = (_, index) =>
        {
            if (index != 2) return;
            if (scenario == "conflict") File.WriteAllText(fixture.Source, "external sentinel");
            if (scenario == "cancelled") cancellation.Cancel();
            if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
            if (scenario == "revoked") roots.Clear();
        } };
        var result = await fixture.WriteAsync(sessions, fixture.Context with { AuthorizedRoots = roots, Timeout = TimeSpan.FromSeconds(1) },
            cancellation.Token, time);
        Assert.Equal(error, result.ErrorCode);
        Assert.Null(result.FileChanges);
        Assert.Equal(2, sessions.Sessions.Count);
        Assert.All(sessions.Sessions, session => { Assert.True(session.IsComplete); Assert.False(session.IsRetained); Assert.False(session.Stream.CanRead); });
        fixture.AssertNoArtifacts();
        Assert.Equal(scenario == "conflict" ? "external sentinel" : "handle fixture", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
        Assert.Equal(fixture.Source, Assert.Single(Directory.GetFiles(fixture.Project)));
    }

    [Fact]
    public async Task SourceChangeAfterInitialHashCannotProduceSnapshotWithIncorrectHash()
    {
        using var fixture = new OwnedArtifactFixture();
        FileStream? source = null;
        var writes = new CallbackAtomicWriteSessions(session => source = session.Stream);
        var sessions = new ObservedArtifactSessions { Created = (_, index) =>
        {
            if (index != 2) return;
            source!.Position = 0;
            source.Write(Encoding.UTF8.GetBytes("changed"));
            source.Flush();
            source.Position = 0;
        } };
        var result = await fixture.WriteAsync(sessions, writes: writes);
        Assert.Equal("file_changed", result.ErrorCode);
        Assert.Null(result.FileChanges);
        Assert.True(sessions.Sessions[0].IsComplete);
        Assert.False(sessions.Sessions[1].IsComplete);
        Assert.All(sessions.Sessions, session => Assert.False(session.Stream.CanRead));
        fixture.AssertNoArtifacts();
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CleanupRefusalStillClosesAndCleansAllOtherSnapshots(int refused)
    {
        using var fixture = new OwnedArtifactFixture();
        string? orphan = null;
        var sessions = new ObservedArtifactSessions { Completed = (session, index) =>
        {
            if (index == refused) { File.SetAttributes(session.Stream.SafeFileHandle, FileAttributes.ReadOnly); orphan = session.Path; }
            if (index == 2) File.WriteAllText(fixture.Source, "external sentinel");
        } };
        try
        {
            var result = await fixture.WriteAsync(sessions);
            Assert.Equal("artifact_cleanup_failed", result.ErrorCode);
            Assert.Null(result.FileChanges);
            Assert.Equal(orphan, Assert.Single(Directory.GetFiles(fixture.ArtifactRoot)));
            Assert.All(sessions.Sessions, session => Assert.False(session.Stream.CanRead));
            Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
        }
        finally { if (orphan is not null) File.SetAttributes(orphan, FileAttributes.Normal); }
    }

    [Fact]
    public async Task UnchangedContentDoesNotCreateSnapshots()
    {
        using var fixture = new OwnedArtifactFixture();
        var sessions = new ObservedArtifactSessions();
        var result = await fixture.WriteAsync(sessions, content: "handle fixture");
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Null(result.FileChanges);
        Assert.Empty(sessions.Sessions);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.ArtifactParent));
    }
}
