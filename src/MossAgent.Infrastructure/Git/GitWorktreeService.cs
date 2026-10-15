using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Git;

public sealed class GitWorktreeService(AppDataPaths paths, GitProcessRunner git) : IGitWorktreeService
{
    public async Task<string> CreateAsync(
        ProjectProfile project,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();
        var target = Path.Combine(paths.WorktreesPath, taskId.ToString("N"));
        var branch = $"mossagent/{taskId:N}";
        var result = await git.RunAsync(
            project.PrimaryDirectory,
            ["worktree", "add", "-b", branch, target, "HEAD"],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"创建 worktree 失败：{result.Output}");
        }

        return target;
    }

    public async Task RemoveAsync(
        ProjectProfile project,
        string worktreePath,
        CancellationToken cancellationToken)
    {
        var managedRoot = Path.GetFullPath(paths.WorktreesPath);
        var target = Path.GetFullPath(worktreePath);
        if (!target.StartsWith(
                Path.TrimEndingDirectorySeparator(managedRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("只能移除 MossAgent 管理的 worktree。");
        }

        var result = await git.RunAsync(
            project.PrimaryDirectory,
            ["worktree", "remove", target],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"移除 worktree 失败：{result.Output}");
        }
    }
}
