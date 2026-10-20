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

internal sealed class GitPushExecutionFixture : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("MossAgent-push-execution-").FullName;
    public GitPushProcessProbe Processes { get; } = new();
    public ManualToolTimeProvider Clock { get; } = new();
    public ToolProgressAuditSink Audit { get; } = new();
    public ToolExecutor Executor { get; }
    public ToolExecutionContext Context { get; }
    public Action? OnApproval { get; set; }

    public GitPushExecutionFixture()
    {
        Context = new(Guid.NewGuid(), Root, [Root], ApprovalPolicy.AskEveryTime, Timeout: TimeSpan.FromSeconds(5));
        var repository = new GitIndexRepositoryProbe { Authorize = (_, _) => Task.FromResult(new ToolGitRepository(Root, Path.Combine(Root, ".git"), Path.Combine(Root, ".git"))) };
        var git = new GitCommandRunner(Processes);
        var states = new GitPushStateReader(new AuthorizedPathResolver(), repository, git, new(new GitPushDestinationProbe()));
        Audit.Release.SetResult();
        Executor = new(new([new GitPushPreviewTool(states), new GitPushTool(states, git)]),
            new PolicyApprovalService((_, _, _) => { OnApproval?.Invoke(); return ValueTask.FromResult(true); }), Audit, Clock);
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, CancellationToken token, ToolExecutionContext? context = null) => Executor.ExecuteAsync(
        new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)), context ?? Context, token);
    public async Task<GitPushPreview> PreviewAsync()
    {
        var result = await ExecuteAsync("git.push_preview", new { }, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitPushPreview>(result.Content!, JsonSerializerOptions.Web)!;
    }

    public void Dispose()
    {
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(Root, recursive: true);
    }
}
