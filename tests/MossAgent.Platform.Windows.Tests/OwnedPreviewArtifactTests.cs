using System.Text.Json;
using MossAgent.Testing;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class OwnedPreviewArtifactTests
{
    [Theory]
    [InlineData("success", null)]
    [InlineData("cancelled", "tool_cancelled")]
    [InlineData("deadline", "tool_timeout")]
    [InlineData("revoked", "path_denied")]
    public async Task CompletedPreviewIsOnlyRetainedAfterFinalAuthorizationAndCancellationChecks(string scenario, string? error)
    {
        using var fixture = new OwnedArtifactFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        var roots = new List<string> { fixture.Project };
        var sessions = new ObservedArtifactSessions { Completed = (_, _) =>
        {
            if (scenario == "cancelled") cancellation.Cancel();
            if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
            if (scenario == "revoked") roots.Clear();
        } };
        var tool = new FilePreviewTool(new(), artifactSessions: sessions);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => throw new InvalidOperationException()), timeProvider: time);
        var result = await executor.ExecuteAsync(new("owned-preview", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { path = fixture.Source })),
            fixture.Context with { AuthorizedRoots = roots, ApprovalPolicy = MossAgent.Domain.ApprovalPolicy.ReadOnly,
                MaximumOutputCharacters = 64, Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.True(result.OutputCharacterCount <= 64);
        var session = Assert.Single(sessions.Sessions);
        Assert.True(session.IsComplete);
        Assert.Equal(error is null, session.IsRetained);
        Assert.False(session.Stream.CanRead);
        if (error is null)
        {
            Assert.Equal(session.Path, Assert.Single(result.Artifacts!).Path);
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(session.Path, fixture.Token));
        }
        else { Assert.Null(result.Artifacts); fixture.AssertNoArtifacts(); }
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
    }
}
