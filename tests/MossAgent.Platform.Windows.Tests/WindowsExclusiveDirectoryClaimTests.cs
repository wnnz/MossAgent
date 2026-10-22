using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Files;
using MossAgent.Platform.Windows.Processes;
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
        var repository = Path.Combine(root, "repository");
        var project = new ProjectProfile(Guid.NewGuid(), "worktree claim", repository, [root], DateTimeOffset.UtcNow);
        var taskId = Guid.NewGuid();
        var data = new AppDataPaths(Path.Combine(root, "data"));
        var raw = new GitProcessRunner(new WindowsTaskProcessLauncher());
        var service = new GitWorktreeService(data, raw,
            exclusiveDirectories: new WindowsExclusiveDirectoryClaimFactory());
        Directory.CreateDirectory(repository);
        try
        {
            Assert.Equal(0, (await raw.RunAsync(repository, ["init"], TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await raw.RunAsync(repository, ["config", "user.name", "MossAgent"],
                TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await raw.RunAsync(repository, ["config", "user.email", "mossagent@example.invalid"],
                TestContext.Current.CancellationToken)).ExitCode);
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "baseline",
                TestContext.Current.CancellationToken);
            Assert.Equal(0, (await raw.RunAsync(repository, ["add", "README.md"], TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await raw.RunAsync(repository, ["commit", "-m", "baseline"], TestContext.Current.CancellationToken)).ExitCode);

            var target = await service.CreateAsync(project, taskId, TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(target));
            Assert.True(File.Exists(Path.Combine(target, ".git")));
            Assert.True(File.Exists(Path.Combine(target, "README.md")));
            await service.RemoveAsync(project, target, TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(target));
        }
        finally { await DeleteRootAsync(root); }
    }

    private static async Task DeleteRootAsync(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        await WindowsProcessTestDirectory.DeleteAsync(root);
    }
}
