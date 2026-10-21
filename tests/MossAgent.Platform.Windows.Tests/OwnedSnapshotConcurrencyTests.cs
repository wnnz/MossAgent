using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class OwnedSnapshotConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherTaskCanCommitWhileFirstOwnsSnapshotsAndFirstFailureOnlyCleansItsOwnFiles(bool cancel)
    {
        using var fixture = new OwnedArtifactFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessions = new ObservedArtifactSessions { AsyncCompleted = async (_, index, token) =>
        {
            if (index != 2) return;
            prepared.TrySetResult();
            await release.Task.WaitAsync(token);
        } };
        var first = fixture.WriteAsync(sessions, token: cancellation.Token);
        try
        {
            await prepared.Task.WaitAsync(fixture.Token);
            var task = Guid.NewGuid();
            var context = new ToolExecutionContext(task, fixture.Project, [fixture.Project], fixture.Context.ApprovalPolicy,
                ArtifactDirectory: Path.Combine(fixture.ArtifactParent, task.ToString("N")));
            var otherSessions = new ObservedArtifactSessions();
            var second = await fixture.WriteAsync(otherSessions, context, content: "other task");
            Assert.True(second.IsSuccess, second.Summary);
            if (cancel) cancellation.Cancel();
            else release.TrySetResult();
            Assert.Equal(cancel ? "tool_cancelled" : "file_changed", (await first.WaitAsync(fixture.Token)).ErrorCode);
            fixture.AssertNoArtifacts();
            Assert.All(sessions.Sessions, session => { Assert.False(session.IsRetained); Assert.False(session.Stream.CanRead); });
            Assert.All(otherSessions.Sessions, session => { Assert.True(session.IsRetained); Assert.False(session.Stream.CanRead); });
            Assert.Equal(2, Directory.GetFiles(context.ArtifactDirectory!).Length);
            var bytes = await new MossAgent.Tools.BuiltIn.Files.FileChangeSnapshotReader().ReadChangeAsync(
                Assert.Single(second.FileChanges!), context, fixture.Token);
            Assert.Equal("other task", System.Text.Encoding.UTF8.GetString(bytes.After));
            Assert.Equal("other task", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
        }
        finally { cancellation.Cancel(); release.TrySetResult(); await first; }
    }
}
