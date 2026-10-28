using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using MossAgent.Tools.BuiltIn.Processes;
using Xunit;

namespace MossAgent.Tools.Tests;

/// <summary>Every context-launched process must carry the task's ProcessIsolationSpec.</summary>
public sealed class ToolIsolationWiringTests
{
    [Fact]
    public async Task ShellRun_AttachesTaskIsolationToTheRequest()
    {
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var tool = new ShellRunTool(new AuthorizedPathResolver(), launcher);
        var executor = new ToolExecutor(new ToolRegistry([tool]),
            new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var root = Path.GetTempPath();
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

        var result = await executor.ExecuteAsync(new ToolRequest("shell", "shell.run",
            JsonSerializer.SerializeToElement(new { command = "Write-Output hello" })), context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Summary);
        var isolation = Assert.IsAssignableFrom<ProcessIsolationSpec>(Assert.Single(launcher.Requests).Isolation);
        Assert.Equal([Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))], isolation.WritableRoots);
    }

    [Fact]
    public async Task GitRunner_AttachesTaskIsolationToTheRequest()
    {
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var runner = new GitCommandRunner(launcher);
        var root = Path.GetTempPath();
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

        var result = await runner.RunAsync(context.WorkingDirectory, ["status"], context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Summary);
        var isolation = Assert.IsAssignableFrom<ProcessIsolationSpec>(Assert.Single(launcher.Requests).Isolation);
        Assert.Equal([Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))], isolation.WritableRoots);
        Assert.Equal(new[] { "GIT_" }, Assert.Single(launcher.Requests).RemovedEnvironmentPrefixes);
    }

    [Fact]
    public async Task GitRunner_ExtraReadOnlyPathsAreMergedIntoIsolation()
    {
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var runner = new GitCommandRunner(launcher);
        var root = Path.GetTempPath();
        var extra = Path.Combine(Path.GetTempPath(), "extra-" + Guid.NewGuid().ToString("N") + ".txt");
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

        await runner.RunAsync(context.WorkingDirectory, ["diff", "--no-index", "--", extra, extra],
            context, TestContext.Current.CancellationToken, extraReadOnlyPaths: [extra]);

        var isolation = Assert.IsAssignableFrom<ProcessIsolationSpec>(Assert.Single(launcher.Requests).Isolation);
        Assert.Equal([extra], isolation.ReadOnlyPaths);
    }
}
