using System.Diagnostics;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeServiceTests
{
    [Fact]
    public async Task RemoveAsync_OnlyRemovesCleanManagedWorktrees()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repository");
        var appData = Path.Combine(root, "app-data");
        Directory.CreateDirectory(repository);
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await InitializeRepositoryAsync(repository, cancellationToken);
            var project = new ProjectProfile(
                Guid.NewGuid(), "test", repository, [repository], DateTimeOffset.UtcNow);
            var service = new GitWorktreeService(new AppDataPaths(appData));
            var worktree = await service.CreateAsync(
                project, Guid.NewGuid(), cancellationToken);
            Assert.True(Directory.Exists(worktree));

            var outside = Path.Combine(root, "outside");
            Directory.CreateDirectory(outside);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.RemoveAsync(project, outside, cancellationToken));

            var dirtyFile = Path.Combine(worktree, "dirty.txt");
            await File.WriteAllTextAsync(dirtyFile, "dirty", cancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RemoveAsync(project, worktree, cancellationToken));
            Assert.True(Directory.Exists(worktree));

            File.Delete(dirtyFile);
            await service.RemoveAsync(project, worktree, cancellationToken);
            Assert.False(Directory.Exists(worktree));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static async Task InitializeRepositoryAsync(
        string repository,
        CancellationToken cancellationToken)
    {
        await RunGitAsync(repository, ["init"], cancellationToken);
        await RunGitAsync(repository, ["config", "user.email", "test@example.invalid"], cancellationToken);
        await RunGitAsync(repository, ["config", "user.name", "MossAgent Tests"], cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(repository, "README.md"), "test", cancellationToken);
        await RunGitAsync(repository, ["add", "README.md"], cancellationToken);
        await RunGitAsync(repository, ["commit", "-m", "initial"], cancellationToken);
    }

    private static async Task RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {await output} {await error}");
    }

    private static void DeleteTree(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
