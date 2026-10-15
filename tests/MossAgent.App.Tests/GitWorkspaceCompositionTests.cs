using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Git;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class GitWorkspaceCompositionTests
{
    [Fact]
    public async Task CompositionRoutesGitToolsAndBackgroundWorktreesThroughTheOwnedLauncherAndAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        var directory = Directory.CreateDirectory(Path.Combine(root, "repository")).FullName;
        try
        {
            await using var services = AppComposition.CreateServices(data);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            Assert.IsType<WindowsTaskProcessLauncher>(services.GetRequiredService<ITaskProcessLauncher>());
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var project = new ProjectProfile(Guid.NewGuid(), "synthetic Git", directory, [root], DateTimeOffset.UtcNow);
            await repository.SaveProjectAsync(project, token);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "synthetic Git workflow", ApprovalPolicy.FullAccess,
                AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            await repository.SaveTaskAsync(task, token);
            var context = new ToolExecutionContext(task.Id, directory, [root], task.ApprovalPolicy);
            await InitializeRepositoryAsync(services.GetRequiredService<GitCommandRunner>(), context, token);
            await File.WriteAllTextAsync(Path.Combine(directory, "added.txt"), "synthetic-added\n", token);
            var executor = services.GetRequiredService<IToolExecutor>();
            var requests = new (string Name, object Arguments)[]
            {
                ("git.status", new { nullTerminated = true }),
                ("git.diff", new { path = "added.txt", untracked = true }),
                ("git.log", new { count = 1 }),
                ("git.create_worktree", new { path = Path.Combine(root, "tool-worktree"), branch = "HEAD" })
            };
            foreach (var request in requests)
            {
                var result = await executor.ExecuteAsync(new ToolRequest(Guid.NewGuid().ToString("N"), request.Name,
                    JsonSerializer.SerializeToElement(request.Arguments)), context, token);
                Assert.True(result.IsSuccess, result.Summary);
            }
            Assert.True(Directory.Exists(Path.Combine(root, "tool-worktree")));
            var audits = await services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(task.Id, 10, token);
            Assert.Equal(4, audits.Count);
            Assert.Equal(requests.Select(request => request.Name).Order(), audits.Select(audit => audit.ToolName).Order());
            Assert.All(audits, audit =>
            {
                Assert.True(audit.IsSuccess);
                Assert.NotNull(audit.CompletedAt);
                Assert.Null(audit.ErrorCode);
            });
            var info = services.GetRequiredService<IGitRepositoryInfoService>();
            Assert.False(string.IsNullOrWhiteSpace(await info.GetBranchNameAsync(directory, token)));
            var worktrees = services.GetRequiredService<IGitWorktreeService>();
            var managed = await worktrees.CreateAsync(project, Guid.NewGuid(), token);
            Assert.StartsWith("mossagent/", await info.GetBranchNameAsync(managed, token));
            await worktrees.RemoveAsync(project, managed, token);
            Assert.False(Directory.Exists(managed));
        }
        finally
        {
            SqliteTestPool.Clear(data);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task InitializeRepositoryAsync(GitCommandRunner git, ToolExecutionContext context, CancellationToken token)
    {
        foreach (var arguments in new string[][]
        {
            ["init"], ["config", "core.autocrlf", "false"], ["config", "user.name", "Git composition QA"],
            ["config", "user.email", "qa@example.invalid"]
        })
            Assert.True((await git.RunAsync(context.WorkingDirectory, arguments, context, token)).IsSuccess);
        await File.WriteAllTextAsync(Path.Combine(context.WorkingDirectory, "baseline.txt"), "synthetic", token);
        Assert.True((await git.RunAsync(context.WorkingDirectory, ["add", "baseline.txt"], context, token)).IsSuccess);
        Assert.True((await git.RunAsync(context.WorkingDirectory, ["commit", "-m", "synthetic baseline"], context, token)).IsSuccess);
    }
}
