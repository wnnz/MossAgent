using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WorktreeTargetClaimTests
{
    [Fact]
    public async Task CreateExclusivelyCreatesEmptyDirectoryAndReportsIdentity()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = Path.Combine(fixture.Root, "claim");
        var factory = new WindowsWorktreeTargetClaimFactory();

        using var claim = await factory.CreateAsync(target, TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        Assert.Equal(target, claim.Path);
        Assert.Equal(32, claim.Identity.FileId.Length);
        Assert.Contains("VOLUME{", claim.Identity.VolumeId);
        claim.Verify();
    }

    [Fact]
    public async Task CreateRejectsExistingDirectoryAndFile()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var factory = new WindowsWorktreeTargetClaimFactory();
        var occupied = fixture.DirectoryFor("occupied");
        var file = fixture.FileFor(fixture.Root, "file-target");

        await Assert.ThrowsAnyAsync<Exception>(() => factory.CreateAsync(occupied, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<Exception>(() => factory.CreateAsync(file, TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(occupied));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task ClaimedTargetAcceptsWritesAndVerifySurvivesContent()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = Path.Combine(fixture.Root, "claim");
        using var claim = await new WindowsWorktreeTargetClaimFactory()
            .CreateAsync(target, TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(Path.Combine(target, ".git"), "gitdir: elsewhere", TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(target, "nested"));

        claim.Verify();
    }

    [Fact]
    public async Task ReplacementOfClaimedDirectoryFailsVerification()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = Path.Combine(fixture.Root, "claim");
        var moved = Path.Combine(fixture.Root, "moved");
        var claim = await new WindowsWorktreeTargetClaimFactory()
            .CreateAsync(target, TestContext.Current.CancellationToken);

        claim.Dispose();
        Directory.Move(target, moved);
        Directory.CreateDirectory(target);

        Assert.Throws<ProjectRootAuthorizationException>(claim.Verify);
    }

    [Fact]
    public async Task ClaimAllowsTargetRemovalWhileHeld()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = Path.Combine(fixture.Root, "claim");
        using var claim = await new WindowsWorktreeTargetClaimFactory()
            .CreateAsync(target, TestContext.Current.CancellationToken);

        Directory.Delete(target, recursive: true);

        Assert.False(Directory.Exists(target));
        Assert.ThrowsAny<Exception>(claim.Verify);
    }
}
