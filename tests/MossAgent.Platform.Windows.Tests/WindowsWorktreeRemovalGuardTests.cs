using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WindowsWorktreeRemovalGuardTests
{
    [Fact]
    public async Task GuardConfirmsWhenTheEnrolledDirectoryObjectWasRemoved()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = fixture.DirectoryFor("worktree");
        var expected = await EnrolledIdentityAsync(target);

        using var guard = await new WindowsWorktreeRemovalGuardFactory()
            .OpenAsync(target, expected, TestContext.Current.CancellationToken);
        Directory.Delete(target);

        Assert.False(Directory.Exists(target));
        guard.ConfirmRemoved();
    }

    [Fact]
    public async Task GuardFailsClosedWhenTheEnrolledObjectSurvives()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = fixture.DirectoryFor("worktree");
        var expected = await EnrolledIdentityAsync(target);
        using var guard = await new WindowsWorktreeRemovalGuardFactory()
            .OpenAsync(target, expected, TestContext.Current.CancellationToken);

        Assert.Throws<ProjectRootAuthorizationException>(guard.ConfirmRemoved);
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public async Task GuardDetectsRemovalOfOnlyASubstitutedDirectory()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = fixture.DirectoryFor("worktree");
        var expected = await EnrolledIdentityAsync(target);
        using var guard = await new WindowsWorktreeRemovalGuardFactory()
            .OpenAsync(target, expected, TestContext.Current.CancellationToken);

        // The guard's delete sharing permits the enrolled object to be renamed away and a
        // replacement deleted under the original name; the enrolled object then survives.
        var swapped = Path.Combine(fixture.Root, "swapped");
        Directory.Move(target, swapped);
        Directory.CreateDirectory(target);
        Directory.Delete(target);

        Assert.Throws<ProjectRootAuthorizationException>(guard.ConfirmRemoved);
        Assert.True(Directory.Exists(swapped));
    }

    [Fact]
    public async Task OpenRejectsReplacedAndMissingTargets()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var target = fixture.DirectoryFor("worktree");
        var expected = await EnrolledIdentityAsync(target);
        Directory.Delete(target);
        Directory.CreateDirectory(target);
        var factory = new WindowsWorktreeRemovalGuardFactory();

        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(
            () => factory.OpenAsync(target, expected, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(
            () => factory.OpenAsync(Path.Combine(fixture.Root, "missing"), expected,
                TestContext.Current.CancellationToken));
    }

    private static async Task<DirectoryObjectIdentity> EnrolledIdentityAsync(string path)
    {
        using var session = await new WindowsRootIdentitySessionFactory()
            .OpenAsync([path], TestContext.Current.CancellationToken);
        return Assert.Single(session.Roots);
    }
}
