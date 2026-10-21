using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Files;
using MossAgent.Testing;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ArtifactWriteHandleTests
{
    [Fact]
    public async Task ReadOnlyPreviewCreatesOneRealAtomicTaskArtifactAndRecordsSourceVersion()
    {
        using var fixture = new ArtifactWriteFixture();
        var result = await fixture.PreviewAsync();
        Assert.True(result.IsSuccess, result.Summary);
        var artifact = Assert.Single(result.Artifacts!);
        Assert.Equal(1, fixture.Sessions.Creates);
        Assert.Equal(fixture.ArtifactRoot, Path.GetDirectoryName(artifact.Path));
        Assert.Equal(artifact.Path, Assert.Single(Directory.GetFiles(fixture.ArtifactRoot)));
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(artifact.Path, fixture.Token));
        Assert.Equal(artifact.Sha256, fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Source))!.Sha256);
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Root));
        Assert.Equal("handle fixture", System.Text.Encoding.UTF8.GetString(await new TaskArtifactContentReader(paths,
            new WindowsFileReadHandleValidator()).ReadAsync(fixture.Context.TaskId, artifact, fixture.Token)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrDeadlineAfterStagingDeletesOnlyItsOwnUncommittedFile(bool deadline)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var time = new ManualToolTimeProvider();
        var sessions = new CallbackAtomicWriteSessions(_ =>
        {
            if (deadline) time.Advance(TimeSpan.FromSeconds(2));
            else cancellation.Cancel();
        });
        using var fixture = new ArtifactWriteFixture(sessions);
        var result = await fixture.PreviewAsync(context: fixture.Context with { Timeout = TimeSpan.FromSeconds(1) }, token: cancellation.Token, time: time);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.Null(result.Artifacts);
        Assert.Empty(Directory.GetFiles(fixture.ArtifactRoot));
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Source, fixture.Token));
    }

    [Fact]
    public async Task ArtifactParentRemainsPinnedThroughNativeCommit()
    {
        ArtifactWriteFixture? fixture = null;
        var sessions = new CallbackAtomicWriteSessions(_ =>
        {
            Assert.Throws<IOException>(() => Directory.Move(fixture!.ArtifactRoot, fixture.ArtifactRoot + "-renamed"));
            Assert.Throws<IOException>(() => Directory.Move(fixture!.ArtifactParent, fixture.ArtifactParent + "-renamed"));
        });
        using var owned = new ArtifactWriteFixture(sessions);
        fixture = owned;
        Assert.True((await fixture.PreviewAsync()).IsSuccess);
        Directory.Move(fixture.ArtifactRoot, fixture.ArtifactRoot + "-renamed");
    }

    [Theory]
    [InlineData("foreign-task")]
    [InlineData("rootless")]
    public async Task InvalidTaskScopeOrSourceAuthorizationCannotCreateArtifact(string scenario)
    {
        using var fixture = new ArtifactWriteFixture();
        var context = fixture.Context with
        {
            ArtifactDirectory = scenario == "foreign-task" ? Path.Combine(fixture.ArtifactParent, Guid.NewGuid().ToString("N")) : fixture.ArtifactRoot,
            AuthorizedRoots = scenario == "rootless" ? [] : fixture.Context.AuthorizedRoots
        };
        var result = await fixture.PreviewAsync(context);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.Sessions.Creates);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.ArtifactParent));
    }

    [Fact]
    public async Task ExistingJunctionAsTaskArtifactRootCannotWriteOutside()
    {
        using var fixture = new ArtifactWriteFixture();
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Root, "outside")).FullName;
        var sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "outside sentinel", fixture.Token);
        await TestDirectoryJunction.CreateAsync(fixture.ArtifactRoot, outside, fixture.Token);
        try
        {
            var result = await fixture.PreviewAsync();
            Assert.Equal("directory_create_unsafe", result.ErrorCode);
            Assert.Equal(0, fixture.Sessions.Creates);
            Assert.Equal(sentinel, Assert.Single(Directory.GetFiles(outside)));
            Assert.Equal("outside sentinel", await File.ReadAllTextAsync(sentinel, fixture.Token));
        }
        finally { Directory.Delete(fixture.ArtifactRoot); }
    }

    [Fact]
    public async Task CancellingOnePreviewDoesNotDeleteAnotherTasksCommittedArtifact()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var sessions = new CallbackAtomicWriteSessions(_ =>
        {
            if (Interlocked.Increment(ref calls) != 1) return;
            prepared.TrySetResult();
            Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).GetAwaiter().GetResult();
        });
        using var fixture = new ArtifactWriteFixture(sessions);
        var first = fixture.PreviewAsync(token: cancellation.Token);
        try
        {
            await prepared.Task.WaitAsync(fixture.Token);
            var secondTask = Guid.NewGuid();
            var otherRoot = Path.Combine(fixture.ArtifactParent, secondTask.ToString("N"));
            var other = new MossAgent.Tools.Abstractions.ToolExecutionContext(secondTask, fixture.Context.WorkingDirectory,
                fixture.Context.AuthorizedRoots, MossAgent.Domain.ApprovalPolicy.ReadOnly, ArtifactDirectory: otherRoot);
            var second = await fixture.PreviewAsync(other);
            Assert.True(second.IsSuccess, second.Summary);
            var artifact = Assert.Single(second.Artifacts!);
            cancellation.Cancel();
            Assert.Equal("tool_cancelled", (await first.WaitAsync(fixture.Token)).ErrorCode);
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(artifact.Path, fixture.Token));
            Assert.Empty(Directory.GetFiles(fixture.ArtifactRoot));
            Assert.Equal(artifact.Path, Assert.Single(Directory.GetFiles(otherRoot)));
        }
        finally { cancellation.Cancel(); }
    }
}
