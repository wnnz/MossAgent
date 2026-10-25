using System.Text.Json;
using MossAgent.Infrastructure.Git;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class GitRemotesFixture : IDisposable
{
    private readonly GitIndexFixture _repository = new();
    public ToolExecutor Executor { get; }
    public ToolProgressAuditSink Audit { get; } = new();
    public int ApprovalRequests { get; private set; }

    public GitRemotesFixture()
    {
        var launcher = new FixtureTaskProcessLauncher();
        var authorization = new GitRepositoryAuthorization(new GitProcessRunner(launcher));
        var reader = new GitRemoteStateReader(new AuthorizedPathResolver(), authorization, new GitCommandRunner(launcher));
        Audit.Release.SetResult();
        Executor = new(new ToolRegistry([new GitRemotesTool(reader)]), new PolicyApprovalService((_, _, _) =>
        { ApprovalRequests++; return ValueTask.FromResult(true); }), Audit);
    }

    public string Root => _repository.Root;
    public ToolExecutionContext Context => _repository.Context;
    public Task InitializeAsync() => _repository.InitializeAsync();
    public Task<string> GitAsync(params string[] arguments) => _repository.GitAsync(arguments);

    public Task<ToolResult> ExecuteAsync(object arguments, ToolExecutionContext? context = null) => Executor.ExecuteAsync(
        new(Guid.NewGuid().ToString("N"), "git.remotes", JsonSerializer.SerializeToElement(arguments)),
        context ?? Context, TestContext.Current.CancellationToken);

    public async Task<GitRemotesReport> ReadAsync(object? arguments = null, ToolExecutionContext? context = null)
    {
        var result = await ExecuteAsync(arguments ?? new { }, context);
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitRemotesReport>(result.Content!, JsonSerializerOptions.Web)!;
    }

    public void Dispose() => _repository.Dispose();
}
