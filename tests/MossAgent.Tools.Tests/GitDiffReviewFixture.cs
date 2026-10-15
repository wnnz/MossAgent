using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;

namespace MossAgent.Tools.Tests;

internal sealed class GitDiffReviewFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    public GitCommandRunner Runner { get; } = new(new FixtureTaskProcessLauncher());
    public ToolExecutionContext Context { get; }
    public ToolExecutor Executor { get; }
    public int ApprovalRequests { get; private set; }

    public GitDiffReviewFixture()
    {
        Directory.CreateDirectory(Root);
        Context = new(Guid.NewGuid(), Root, [Root], ApprovalPolicy.ReadOnly);
        var resolver = new AuthorizedPathResolver();
        Executor = new(new ToolRegistry([new GitDiffTool(resolver, Runner), new GitStatusTool(resolver, Runner)]),
            new PolicyApprovalService((_, _, _) => { ApprovalRequests++; return ValueTask.FromResult(true); }));
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        await RunSetupAsync(["init"], token);
        await RunSetupAsync(["config", "core.autocrlf", "false"], token);
    }

    public async Task RunSetupAsync(string[] arguments, CancellationToken token)
    {
        var result = await Runner.RunAsync(Root, arguments, Context, token);
        if (!result.IsSuccess) throw new InvalidOperationException(result.Summary);
    }

    public Task<ToolResult> ExecuteAsync(object arguments, CancellationToken token, ToolExecutionContext? context = null,
        string toolName = "git.diff") => Executor.ExecuteAsync(
        new ToolRequest(Guid.NewGuid().ToString("N"), toolName, JsonSerializer.SerializeToElement(arguments)), context ?? Context, token);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        Directory.Delete(Root, recursive: true);
    }
}
