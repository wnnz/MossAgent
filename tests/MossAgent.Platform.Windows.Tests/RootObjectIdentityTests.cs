using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class RootObjectIdentityTests
{
    [Fact]
    public async Task SessionPinsActualRootAndAReplacementHasDifferentIdentity()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("authorized");
        var moved = Path.Combine(fixture.Root, "original");
        var sessions = new WindowsRootIdentitySessionFactory();
        var session = await sessions.OpenAsync([root], TestContext.Current.CancellationToken);
        var identity = Assert.Single(session.Roots);
        try
        {
            Assert.Equal(root, identity.Path);
            Assert.Equal(32, identity.FileId.Length);
            Assert.Contains("VOLUME{", identity.VolumeId);
            using var second = await sessions.OpenAsync([root], TestContext.Current.CancellationToken);
            Assert.Equal(identity, Assert.Single(second.Roots));
            Assert.Throws<IOException>(() => Directory.Move(root, moved));
        }
        finally { session.Dispose(); }
        Directory.Move(root, moved);
        Directory.CreateDirectory(root);
        using var replacement = await sessions.OpenAsync([root], TestContext.Current.CancellationToken);
        var changed = Assert.Single(replacement.Roots);
        Assert.Equal(identity.VolumeId, changed.VolumeId);
        Assert.NotEqual(identity.FileId, changed.FileId);
    }

    [Fact]
    public async Task MultipleRootsAreOwnedIndependentlyAndReleasedOnDispose()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var roots = new[] { fixture.DirectoryFor("one"), fixture.DirectoryFor("two") };
        var factory = new WindowsRootIdentitySessionFactory();
        var session = await factory.OpenAsync(roots, TestContext.Current.CancellationToken);
        Assert.Equal(roots, session.Roots.Select(item => item.Path));
        foreach (var root in roots) Assert.Throws<IOException>(() => Directory.Move(root, root + "-moved"));
        session.Dispose();
        session.Dispose();
        foreach (var root in roots) Directory.Move(root, root + "-moved");
    }

    [Fact]
    public async Task FailureOrCancellationReleasesAlreadyOpenedRoots()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("valid");
        var factory = new WindowsRootIdentitySessionFactory();
        await Assert.ThrowsAsync<FileWriteSessionException>(() => factory.OpenAsync([root, Path.Combine(fixture.Root, "missing")],
            TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.OpenAsync([root], cancelled.Token));
        Directory.Move(root, root + "-moved");
    }
}
