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

internal sealed class GitHunksFixture : IDisposable
{
    private readonly GitDiffReviewFixture _git = new();
    public string Root => _git.Root;
    public ToolExecutionContext Context => _git.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
    public ToolExecutor Executor { get; }
    public int ApprovalRequests { get; private set; }
    public bool Approved { get; set; } = true;

    public GitHunksFixture()
    {
        var paths = new GitIndexPaths(new AuthorizedPathResolver());
        var authorization = new GitRepositoryAuthorization(new GitProcessRunner(new FixtureTaskProcessLauncher()));
        var hunks = new GitHunkApplier(paths, authorization, _git.Runner);
        Executor = new(new ToolRegistry([new GitStageHunksTool(hunks), new GitUnstageHunksTool(hunks)]),
            new PolicyApprovalService((_, _, _) => { ApprovalRequests++; return ValueTask.FromResult(Approved); }));
    }

    public async Task InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _git.InitializeAsync(token);
        await _git.RunSetupAsync(["config", "core.fsmonitor", "false"], token);
        await _git.RunSetupAsync(["config", "core.hooksPath", Path.Combine(Root, "disabled-test-hooks")], token);
        await _git.RunSetupAsync(["config", "user.name", "MossAgent fixture"], token);
        await _git.RunSetupAsync(["config", "user.email", "fixture@example.invalid"], token);
        await _git.RunSetupAsync(["config", "commit.gpgsign", "false"], token);
    }

    public async Task CommitFileAsync(string name, string content)
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(Root, name), content, token);
        await GitAsync("add", "--", name);
        await GitAsync("commit", "-qm", "fixture " + name);
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, ToolExecutionContext? context = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)),
            context ?? Context, TestContext.Current.CancellationToken);

    public async Task<string> GitAsync(params string[] arguments)
    {
        var result = await GitRawAsync(arguments);
        Assert.True(result.IsSuccess, result.Summary);
        return result.Content ?? string.Empty;
    }

    public Task<ToolResult> GitRawAsync(params string[] arguments) =>
        _git.Runner.RunAsync(Root, arguments, Context, TestContext.Current.CancellationToken);

    public void Dispose() => _git.Dispose();
}
