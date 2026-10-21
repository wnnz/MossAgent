using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class BrowserArtifactConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingOneTaskDuringSdkCaptureOrTransferPreservesOtherTasksCommittedArtifact(bool download)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Files.Token);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Browser.Capture = token => HoldAsync(prepared, token);
        fixture.Browser.Transfer = async (stream, token) => { await stream.WriteAsync(new byte[64], token); await HoldAsync(prepared, token); };
        var first = fixture.ExecuteAsync(download, token: cancellation.Token);
        try
        {
            await prepared.Task.WaitAsync(fixture.Files.Token);
            var task = Guid.NewGuid();
            var other = new BrowserArtifactTestSession();
            var context = new ToolExecutionContext(task, fixture.Files.Project, [fixture.Files.Project], fixture.Context.ApprovalPolicy,
                ArtifactDirectory: Path.Combine(fixture.Files.ArtifactParent, task.ToString("N")), BrowserSession: other);
            var second = await fixture.ExecuteAsync(download, context);
            Assert.True(second.IsSuccess, second.Summary);
            var artifact = Assert.Single(second.Artifacts!);
            var original = await File.ReadAllBytesAsync(artifact.Path, fixture.Files.Token);
            cancellation.Cancel();
            Assert.Equal("tool_cancelled", (await first.WaitAsync(fixture.Files.Token)).ErrorCode);
            fixture.Files.AssertNoArtifacts();
            Assert.Equal(artifact.Path, Assert.Single(Directory.GetFiles(context.ArtifactDirectory!)));
            Assert.Equal(original, await File.ReadAllBytesAsync(artifact.Path, fixture.Files.Token));
            Assert.Equal(1, other.Downloads + other.Captures);
            Assert.All(fixture.Sessions.Sessions, session => Assert.False(session.Stream.CanRead));
        }
        finally { cancellation.Cancel(); await first; }
    }

    private static async Task HoldAsync(TaskCompletionSource prepared, CancellationToken token)
    {
        prepared.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
    }
}
