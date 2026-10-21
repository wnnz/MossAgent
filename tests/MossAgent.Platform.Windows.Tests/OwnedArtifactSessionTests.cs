using System.Text;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class OwnedArtifactSessionTests
{
    [Theory]
    [InlineData("pending")]
    [InlineData("complete")]
    [InlineData("retained")]
    public async Task CompletionKeepsExclusiveOwnershipUntilCallerRetainsOrDisposes(string state)
    {
        using var fixture = new OwnedArtifactFixture();
        var session = await fixture.Native.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token);
        var stream = session.Stream;
        try
        {
            Assert.Throws<InvalidOperationException>(session.Retain);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("owned bytes"), fixture.Token);
            if (state != "pending")
            {
                await session.CompleteAsync(fixture.Token);
                Assert.Throws<IOException>(() => File.Delete(session.Path));
                Assert.Throws<IOException>(() => File.Move(session.Path, session.Path + ".foreign"));
                Assert.Throws<IOException>(() => File.ReadAllText(session.Path));
                await Assert.ThrowsAsync<InvalidOperationException>(() => session.CompleteAsync(fixture.Token));
            }
            Assert.Throws<IOException>(() => Directory.Move(fixture.ArtifactRoot, fixture.ArtifactRoot + "-foreign"));
            Assert.Throws<IOException>(() => Directory.Move(fixture.ArtifactParent, fixture.ArtifactParent + "-foreign"));
            if (state == "retained") session.Retain();
        }
        finally { session.Dispose(); }
        Assert.False(stream.CanRead);
        session.Dispose();
        if (state == "retained") Assert.Equal("owned bytes", await File.ReadAllTextAsync(session.Path, fixture.Token));
        else fixture.AssertNoArtifacts();
        Directory.Move(fixture.ArtifactRoot, fixture.ArtifactRoot + "-released");
    }

    [Fact]
    public async Task FinalNameCollisionDoesNotOverwriteOrDeleteTheOtherFile()
    {
        using var fixture = new OwnedArtifactFixture();
        var session = await fixture.Native.CreateAsync(fixture.Context, "review", ".diff", fixture.Token);
        await File.WriteAllTextAsync(session.Path, "foreign sentinel", fixture.Token);
        try
        {
            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => session.CompleteAsync(fixture.Token));
            Assert.Equal("artifact_write_conflict", error.ErrorCode);
        }
        finally { session.Dispose(); }
        Assert.Equal("foreign sentinel", await File.ReadAllTextAsync(session.Path, fixture.Token));
        Assert.Equal(session.Path, Assert.Single(Directory.GetFiles(fixture.ArtifactRoot)));
    }

    [Fact]
    public async Task RealCleanupRefusalClosesHandleAndReportsRetainedSideEffect()
    {
        using var fixture = new OwnedArtifactFixture();
        var session = await fixture.Native.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token);
        await session.CompleteAsync(fixture.Token);
        File.SetAttributes(session.Stream.SafeFileHandle, FileAttributes.ReadOnly);
        try
        {
            var error = Assert.Throws<FileMutationSessionException>(session.Dispose);
            Assert.Equal("artifact_cleanup_failed", error.ErrorCode);
            Assert.True(error.HasSideEffects);
            Assert.False(session.Stream.CanRead);
            session.Dispose();
            Assert.True(File.Exists(session.Path));
        }
        finally { File.SetAttributes(session.Path, FileAttributes.Normal); }
    }

    [Fact]
    public async Task CancellingOneCompletedSessionKeepsAnotherTasksRetainedFile()
    {
        using var fixture = new OwnedArtifactFixture();
        var task = Guid.NewGuid();
        var context = fixture.Context with { TaskId = task, ArtifactDirectory = Path.Combine(fixture.ArtifactParent, task.ToString("N")) };
        var first = await fixture.Native.CreateAsync(fixture.Context, "preview", ".snapshot", fixture.Token);
        using (first)
        {
            await first.CompleteAsync(fixture.Token);
            using var other = await fixture.Native.CreateAsync(context, "preview", ".snapshot", fixture.Token);
            await other.Stream.WriteAsync(Encoding.UTF8.GetBytes("other task"), fixture.Token);
            await other.CompleteAsync(fixture.Token);
            other.Retain();
        }
        fixture.AssertNoArtifacts();
        Assert.Equal("other task", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(context.ArtifactDirectory!)), fixture.Token));
    }
}
