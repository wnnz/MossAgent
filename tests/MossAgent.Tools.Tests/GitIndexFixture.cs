using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class GitIndexFixture : IDisposable
{
    private readonly GitDiffReviewFixture _git = new();
    public string Root => _git.Root;
    public ToolExecutionContext Context => _git.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
    public ToolExecutor Executor { get; }
    public ToolProgressAuditSink Audit { get; } = new();
    public bool Approved { get; set; } = true;
    public Func<Task>? OnApproval { get; set; }
    public int ApprovalRequests { get; private set; }

    public GitIndexFixture()
    {
        var paths = new GitIndexPaths(new AuthorizedPathResolver());
        var authorization = new GitRepositoryAuthorization(new GitProcessRunner(new FixtureTaskProcessLauncher()));
        var states = new GitCommitStateReader(new AuthorizedPathResolver(), authorization, _git.Runner);
        var tools = new ToolRegistry([new GitStageFileTool(paths, authorization, _git.Runner), new GitUnstageFileTool(paths, authorization, _git.Runner),
            new GitCommitPreviewTool(states), new GitCommitTool(states, _git.Runner)]);
        Audit.Release.SetResult();
        Executor = new(tools, new PolicyApprovalService(async (_, _, _) =>
        { ApprovalRequests++; if (OnApproval is not null) await OnApproval(); return Approved; }), Audit);
    }

    public async Task InitializeAsync(bool commit = true)
    {
        var token = TestContext.Current.CancellationToken;
        await _git.InitializeAsync(token);
        await _git.RunSetupAsync(["config", "core.fsmonitor", "false"], token);
        await _git.RunSetupAsync(["config", "core.hooksPath", Path.Combine(Root, "disabled-test-hooks")], token);
        await _git.RunSetupAsync(["config", "user.name", "MossAgent fixture"], token);
        await _git.RunSetupAsync(["config", "user.email", "fixture@example.invalid"], token);
        await _git.RunSetupAsync(["config", "commit.gpgsign", "false"], token);
        if (!commit) return;
        await File.WriteAllTextAsync(Path.Combine(Root, "tracked.txt"), "baseline\n", token);
        await _git.RunSetupAsync(["add", "--", "tracked.txt"], token);
        await _git.RunSetupAsync(["-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "-m", "fixture"], token);
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, ToolExecutionContext? context = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)),
            context ?? Context, TestContext.Current.CancellationToken);

    public async Task<string> GitAsync(params string[] arguments)
    {
        var result = await _git.Runner.RunAsync(Root, arguments, Context, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Summary);
        return result.Content ?? string.Empty;
    }

    public void Dispose() => _git.Dispose();
}
