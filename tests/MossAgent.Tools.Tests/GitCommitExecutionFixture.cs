using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class GitCommitExecutionFixture : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("MossAgent-commit-execution-").FullName;
    public GitCommitProcessProbe Processes { get; } = new();
    public GitIndexRepositoryProbe Repository { get; } = new();
    public ManualToolTimeProvider Clock { get; } = new();
    public ToolProgressAuditSink Audit { get; } = new();
    public ToolExecutor Executor { get; }
    public Action? OnApproval { get; set; }
    public ToolExecutionContext Context => new(Guid.NewGuid(), Root, [Root], ApprovalPolicy.AskEveryTime, Timeout: TimeSpan.FromSeconds(5));

    public GitCommitExecutionFixture()
    {
        var git = new GitCommandRunner(Processes);
        var states = new GitCommitStateReader(new AuthorizedPathResolver(), Repository, git);
        Audit.Release.SetResult();
        Executor = new(new([new GitCommitPreviewTool(states), new GitCommitTool(states, git)]),
            new PolicyApprovalService((_, _, _) => { OnApproval?.Invoke(); return ValueTask.FromResult(true); }), Audit, Clock);
    }

    public async Task<string> PreviewAsync()
    {
        var result = await ExecuteAsync("git.commit_preview", new { }, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitCommitPreview>(result.Content!, JsonSerializerOptions.Web)!.State;
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, CancellationToken token, ToolExecutionContext? context = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)), context ?? Context, token);

    public void Dispose()
    {
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(Root, recursive: true);
    }
}
