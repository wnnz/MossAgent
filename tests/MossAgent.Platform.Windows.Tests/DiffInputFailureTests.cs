using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DiffInputFailureTests
{
    [Theory]
    [InlineData(false, "refused", "diff_input_refused")]
    [InlineData(true, "refused", "diff_input_refused")]
    [InlineData(false, "revoked", "path_denied")]
    [InlineData(true, "revoked", "path_denied")]
    [InlineData(false, "cancelled", "tool_cancelled")]
    [InlineData(true, "cancelled", "tool_cancelled")]
    [InlineData(false, "deadline", "tool_timeout")]
    [InlineData(true, "deadline", "tool_timeout")]
    public async Task FailureBeforeGitReleasesEveryInputWithoutFallbackOrPublishedOutput(bool statistics, string scenario, string error)
    {
        using var fixture = await DiffInputFlowFixture.CreateAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Files.Token);
        var time = new ManualToolTimeProvider();
        var revoked = false;
        var processes = new DiffInputProcessLauncher();
        fixture.Processes = processes;
        fixture.Inputs.Creating = index =>
        { if (index == 2 && scenario == "refused") throw new FileMutationSessionException("safe input refusal", "diff_input_refused"); };
        fixture.Inputs.Created = (_, index) =>
        {
            if (index != 2) return;
            if (scenario == "revoked") revoked = true;
            if (scenario == "cancelled") cancellation.Cancel();
            if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
        };
        var guard = new CallbackCopyWorkspaceGuard(() => { if (revoked) throw new UnauthorizedAccessException(); });
        var result = await fixture.ExecuteAsync(statistics, fixture.Files.Context with { WorkspaceGuard = guard, Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token, time);
        Assert.Equal(error, result.ErrorCode);
        Assert.Null(result.Content);
        Assert.Null(result.Artifacts);
        Assert.Equal(0, processes.Starts);
        Assert.Equal(scenario == "refused" ? 1 : 2, fixture.Inputs.Sessions.Count);
        Assert.All(fixture.Inputs.Sessions, session => { Assert.True(session.IsDisposed); Assert.False(File.Exists(session.Path)); });
        Assert.Equal(2, Directory.GetFiles(fixture.Files.ArtifactRoot).Length);
        Assert.Empty(Directory.GetDirectories(fixture.Files.ArtifactRoot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessStartFailureAndOutputLimitNeverLeaveInputsOrSuccessfulPatch(bool statistics)
    {
        using var fixture = await DiffInputFlowFixture.CreateAsync();
        fixture.Processes = new DiffInputProcessLauncher { Starting = _ => throw new IOException("private process error") };
        var failed = await fixture.ExecuteAsync(statistics);
        Assert.Equal("tool_failed", failed.ErrorCode);
        Assert.DoesNotContain("private", failed.Summary);
        fixture.Processes = new DiffInputProcessLauncher();
        var limited = await fixture.ExecuteAsync(statistics, fixture.Files.Context with { MaximumOutputCharacters = 16 });
        Assert.Equal("snapshot_review_too_large", limited.ErrorCode);
        Assert.All(fixture.Inputs.Sessions, session => { Assert.True(session.IsDisposed); Assert.False(File.Exists(session.Path)); });
        Assert.Equal(2, Directory.GetFiles(fixture.Files.ArtifactRoot).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupRefusalRemainsVisibleAndDoesNotSkipTheOtherInput(bool statistics)
    {
        using var fixture = await DiffInputFlowFixture.CreateAsync();
        fixture.Inputs.Created = (session, index) =>
        { if (index == 2) session.Releasing = () => File.SetAttributes(session.Path, FileAttributes.ReadOnly); };
        try
        {
            var result = await fixture.ExecuteAsync(statistics);
            Assert.Equal("diff_input_cleanup_failed", result.ErrorCode);
            Assert.Contains("清理未获确认", result.Summary);
            Assert.Null(result.Content);
            Assert.Null(result.Artifacts);
            Assert.All(fixture.Inputs.Sessions, session => Assert.True(session.IsDisposed));
            Assert.False(File.Exists(fixture.Inputs.Sessions[0].Path));
        }
        finally
        {
            foreach (var session in fixture.Inputs.Sessions)
                if (File.Exists(session.Path)) File.SetAttributes(session.Path, FileAttributes.Normal);
        }
    }
}
