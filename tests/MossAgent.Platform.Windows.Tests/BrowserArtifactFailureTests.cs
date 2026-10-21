using MossAgent.Testing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class BrowserArtifactFailureTests
{
    [Theory]
    [InlineData(false, "cancelled", "tool_cancelled")]
    [InlineData(true, "cancelled", "tool_cancelled")]
    [InlineData(false, "deadline", "tool_timeout")]
    [InlineData(true, "deadline", "tool_timeout")]
    [InlineData(false, "revoked", "path_denied")]
    [InlineData(true, "revoked", "path_denied")]
    public async Task FinalCheckFailureCleansCompletedOutputAndDownloadStaging(bool download, string scenario, string error)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Files.Token);
        var time = new ManualToolTimeProvider();
        var revoked = false;
        fixture.Sessions = new ObservedArtifactSessions { Completed = (_, _) =>
        {
            if (scenario == "cancelled") cancellation.Cancel();
            if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
            if (scenario == "revoked") revoked = true;
        } };
        var guard = new CallbackCopyWorkspaceGuard(() => { if (revoked) throw new UnauthorizedAccessException(); });
        var result = await fixture.ExecuteAsync(download, fixture.Context with { WorkspaceGuard = guard, Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token, time);
        Assert.Equal(error, result.ErrorCode);
        Assert.Null(result.Artifacts);
        Assert.All(fixture.Sessions.Sessions, session => { Assert.False(session.IsRetained); Assert.False(session.Stream.CanRead); });
        fixture.Files.AssertNoArtifacts();
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Files.Source, fixture.Files.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedSdkOutputIsRejectedWithoutKeepingPartialFiles(bool download)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        if (!download) fixture.Browser.ScreenshotBytes = new byte[32 * 1024 * 1024 + 1];
        else fixture.Browser.Transfer = async (stream, token) =>
        {
            var block = new byte[1024 * 1024];
            for (var index = 0; index < 32; index++) await stream.WriteAsync(block, token);
            await stream.WriteAsync(new byte[1], token);
        };
        var result = await fixture.ExecuteAsync(download);
        Assert.Equal("browser_artifact_too_large", result.ErrorCode);
        Assert.Null(result.Artifacts);
        Assert.All(fixture.Sessions.Sessions, session => Assert.False(session.Stream.CanRead));
        fixture.Files.AssertNoArtifacts();
    }

    [Fact]
    public async Task EmptyScreenshotCannotBeReportedAsSuccessfulArtifact()
    {
        using var fixture = new BrowserArtifactFlowFixture();
        fixture.Browser.ScreenshotBytes = [];
        var result = await fixture.ExecuteAsync(false);
        Assert.Equal("browser_screenshot_empty", result.ErrorCode);
        fixture.Files.AssertNoArtifacts();
    }

    [Fact]
    public async Task DownloadStagingCleanupFailureAlsoCleansCompletedFinalArtifact()
    {
        using var fixture = new BrowserArtifactFlowFixture();
        string? orphan = null;
        fixture.Sessions = new ObservedArtifactSessions { Created = (session, index) =>
        {
            if (index != 1) return;
            File.SetAttributes(session.Stream.SafeFileHandle, FileAttributes.ReadOnly);
            orphan = Assert.Single(Directory.GetFiles(fixture.Files.ArtifactRoot));
        } };
        try
        {
            var result = await fixture.ExecuteAsync(true);
            Assert.Equal("artifact_cleanup_failed", result.ErrorCode);
            Assert.Null(result.Artifacts);
            Assert.Equal(orphan, Assert.Single(Directory.GetFiles(fixture.Files.ArtifactRoot)));
            Assert.All(fixture.Sessions.Sessions, session => { Assert.False(session.IsRetained); Assert.False(session.Stream.CanRead); });
        }
        finally { if (orphan is not null) File.SetAttributes(orphan, FileAttributes.Normal); }
    }
}
