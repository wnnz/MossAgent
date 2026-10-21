using System.Text.Json;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class OwnedReviewArtifactTests
{
    [Theory]
    [InlineData("success", null)]
    [InlineData("cancelled", "tool_cancelled")]
    [InlineData("deadline", "tool_timeout")]
    [InlineData("revoked", "path_denied")]
    [InlineData("cleanup-refused", "tool_cancelled")]
    public async Task FinalPatchRemainsOwnedThroughFinalChecksAndRetainsOriginalSnapshots(string scenario, string? error)
    {
        using var fixture = new OwnedArtifactFixture();
        var edited = await fixture.WriteAsync(fixture.Native);
        Assert.True(edited.IsSuccess, edited.Summary);
        var change = Assert.Single(edited.FileChanges!);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        var roots = new List<string> { fixture.Project };
        string? orphan = null;
        var sessions = new ObservedArtifactSessions { Completed = (session, _) =>
        {
            Assert.Throws<IOException>(() => File.Move(session.Path, session.Path + ".foreign"));
            if (scenario == "cleanup-refused") { orphan = session.Path; File.SetAttributes(session.Stream.SafeFileHandle, FileAttributes.ReadOnly); }
            if (scenario is "cancelled" or "cleanup-refused") cancellation.Cancel();
            if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
            if (scenario == "revoked") roots.Clear();
        } };
        try
        {
            var result = await ReviewAsync(fixture, change, sessions, roots, cancellation.Token, time);
            Assert.Equal(error, result.ErrorCode);
            var session = Assert.Single(sessions.Sessions);
            Assert.True(session.IsComplete);
            Assert.Equal(error is null, session.IsRetained);
            Assert.False(session.Stream.CanRead);
            if (error is null)
            {
                var patch = Assert.Single(result.Artifacts!);
                Assert.Equal(session.Path, patch.Path);
                var text = await File.ReadAllTextAsync(patch.Path, fixture.Token);
                Assert.Contains("-handle fixture", text);
                Assert.Contains("+updated", text);
            }
            else Assert.Null(result.Artifacts);
            if (scenario == "cleanup-refused") Assert.Contains("清理未能完成", result.Summary);
            Assert.Equal(error is null || orphan is not null ? 3 : 2, Directory.GetFiles(fixture.ArtifactRoot).Length);
            Assert.Empty(Directory.GetDirectories(fixture.ArtifactRoot));
            var bytes = await new FileChangeSnapshotReader().ReadChangeAsync(change, fixture.Context, fixture.Token);
            Assert.Equal("updated", System.Text.Encoding.UTF8.GetString(bytes.After));
        }
        finally { if (orphan is not null) File.SetAttributes(orphan, FileAttributes.Normal); }
    }

    private static Task<ToolResult> ReviewAsync(OwnedArtifactFixture fixture, ToolFileChange change,
        ObservedArtifactSessions sessions, List<string> roots, CancellationToken token, TimeProvider time)
    {
        var tool = new FileChangeReviewTool(new(), new GitCommandRunner(new FixtureTaskProcessLauncher()), sessions);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), timeProvider: time);
        return executor.ExecuteAsync(new("owned-review", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { change })),
            fixture.Context with { ReviewedChange = change, AuthorizedRoots = roots, Timeout = TimeSpan.FromSeconds(1) }, token);
    }
}
