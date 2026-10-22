using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Files;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WindowsExclusiveDirectoryClaimTests
{
    [Fact]
    public async Task ClaimedDirectoryCannotBeReplacedUntilHandleIsReleased()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "worktree");
        var moved = Path.Combine(root, "moved");
        Directory.CreateDirectory(root);
        try
        {
            var factory = new WindowsExclusiveDirectoryClaimFactory();
            using (var claim = factory.Create(target, [root], TestContext.Current.CancellationToken))
            {
                Assert.True(Directory.Exists(target));
                Assert.ThrowsAny<IOException>(() => Directory.Move(target, moved));
                Assert.ThrowsAny<FileMutationSessionException>(() =>
                    factory.Create(target, [root], TestContext.Current.CancellationToken));
                claim.Validate();
            }
            Directory.Move(target, moved);
            Assert.False(Directory.Exists(target));
            Assert.True(Directory.Exists(moved));
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task RealGitInitializesInsideTheExclusiveClaimedDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var taskId = Guid.NewGuid();
        try
        {
            var (project, data, raw) = await CreateRepositoryAsync(root);
            var cleanups = new InMemoryWorktreeCleanupIntentService();
            var service = CreateService(data, raw, cleanupIntents: cleanups);
            var target = await service.CreateAsync(project, taskId, TestContext.Current.CancellationToken);
            Assert.True(Directory.Exists(target));
            Assert.True(File.Exists(Path.Combine(target, ".git")));
            Assert.True(File.Exists(Path.Combine(target, "README.md")));
            await service.RemoveAsync(project, target, TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(target));
            Assert.Equal(MossAgent.Domain.WorktreeCleanupState.Completed,
                cleanups.Last?.State);
            Assert.DoesNotContain(target, (await raw.RunAsync(project.PrimaryDirectory,
                ["worktree", "list", "--porcelain"], TestContext.Current.CancellationToken)).StandardOutput);
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task ReplacementAtOriginalPathIsNeverDeletedDuringGitRegistrationHandoff()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var taskId = Guid.NewGuid();
        try
        {
            var (project, data, raw) = await CreateRepositoryAsync(root);
            var target = Path.Combine(data.WorktreesPath, taskId.ToString("N"));
            var replacement = Path.Combine(target, "replacement.txt");
            var quarantineSessions = new ReplacingQuarantineSessionFactory(
                new WindowsPathQuarantineSessionFactory(), () =>
                {
                    Directory.CreateDirectory(target);
                    File.WriteAllText(replacement, "replacement");
                });
            var service = CreateService(data, raw, quarantineSessions);
            Assert.Equal(target, await service.CreateAsync(
                project, taskId, TestContext.Current.CancellationToken));

            await service.RemoveAsync(project, target, TestContext.Current.CancellationToken);

            Assert.Equal("replacement", await File.ReadAllTextAsync(
                replacement, TestContext.Current.CancellationToken));
            var registrations = await raw.RunAsync(project.PrimaryDirectory,
                ["worktree", "list", "--porcelain"], TestContext.Current.CancellationToken);
            Assert.DoesNotContain(target, registrations.StandardOutput);
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task LockedGitRegistrationRetainsTheWorktreeUntilExplicitlyUnlocked()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var (project, data, raw) = await CreateRepositoryAsync(root);
            var service = CreateService(data, raw);
            var target = await service.CreateAsync(
                project, Guid.NewGuid(), TestContext.Current.CancellationToken);
            await AssertGitAsync(raw, project.PrimaryDirectory, "worktree", "lock", target);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RemoveAsync(project, target, TestContext.Current.CancellationToken));
            Assert.True(File.Exists(Path.Combine(target, ".git")));
            Assert.Contains(target.Replace('\\', '/'), (await raw.RunAsync(project.PrimaryDirectory,
                ["worktree", "list", "--porcelain"], TestContext.Current.CancellationToken)).StandardOutput);
        }
        finally { await DeleteRootAsync(root); }
    }

    [Fact]
    public async Task FileCleanupFailurePersistsTheQuarantinePathAndStage()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var (project, data, raw) = await CreateRepositoryAsync(root);
            var cleanups = new InMemoryWorktreeCleanupIntentService();
            var sessions = new ReplacingQuarantineSessionFactory(
                new WindowsPathQuarantineSessionFactory(), static () => { },
                new IOException("synthetic cleanup failure"));
            var service = CreateService(data, raw, sessions, cleanups);
            var target = await service.CreateAsync(
                project, Guid.NewGuid(), TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<IOException>(
                () => service.RemoveAsync(project, target, TestContext.Current.CancellationToken));

            var intent = Assert.IsType<MossAgent.Domain.WorktreeCleanupIntent>(cleanups.Last);
            Assert.Equal(MossAgent.Domain.WorktreeCleanupState.Failed, intent.State);
            Assert.Equal(MossAgent.Domain.WorktreeCleanupStage.RegistrationDeleted, intent.Stage);
            Assert.True(Directory.Exists(intent.QuarantinePath));
            Assert.False(Directory.Exists(target));
            Assert.DoesNotContain(target,
                (await raw.RunAsync(project.PrimaryDirectory,
                    ["worktree", "list", "--porcelain"],
                    TestContext.Current.CancellationToken)).StandardOutput);
        }
        finally { await DeleteRootAsync(root); }
    }

    private static GitWorktreeService CreateService(
        AppDataPaths data, GitProcessRunner git,
        IPathQuarantineSessionFactory? quarantineSessions = null,
        InMemoryWorktreeCleanupIntentService? cleanupIntents = null) =>
        new(data, git,
            exclusiveDirectories: new WindowsExclusiveDirectoryClaimFactory(),
            quarantineSessions: quarantineSessions ?? new WindowsPathQuarantineSessionFactory(),
            deleteSessions: new WindowsPathDeleteSessionFactory(),
            cleanupIntents: cleanupIntents ?? new InMemoryWorktreeCleanupIntentService());

    private static async Task<(ProjectProfile Project, AppDataPaths Data, GitProcessRunner Git)>
        CreateRepositoryAsync(string root)
    {
        var repository = Path.Combine(root, "repository");
        var project = new ProjectProfile(Guid.NewGuid(), "worktree claim", repository,
            [root], DateTimeOffset.UtcNow);
        var data = new AppDataPaths(Path.Combine(root, "data"));
        var raw = new GitProcessRunner(new WindowsTaskProcessLauncher());
        Directory.CreateDirectory(repository);
        await AssertGitAsync(raw, repository, "init");
        await AssertGitAsync(raw, repository, "config", "user.name", "MossAgent");
        await AssertGitAsync(raw, repository, "config", "user.email",
            "mossagent@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "baseline",
            TestContext.Current.CancellationToken);
        await AssertGitAsync(raw, repository, "add", "README.md");
        await AssertGitAsync(raw, repository, "commit", "-m", "baseline");
        return (project, data, raw);
    }

    private static async Task AssertGitAsync(
        GitProcessRunner git, string directory, params string[] arguments)
    {
        var result = await git.RunAsync(
            directory, arguments, TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }

    private static async Task DeleteRootAsync(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        await WindowsProcessTestDirectory.DeleteAsync(root);
    }
}
