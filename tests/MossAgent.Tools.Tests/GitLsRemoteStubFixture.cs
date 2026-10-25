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

internal sealed class GitLsRemoteStubFixture : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("MossAgent-ls-remote-stub-").FullName;
    public GitLsRemoteProcessProbe Processes { get; } = new();
    public ManualToolTimeProvider Clock { get; } = new();
    public ToolProgressAuditSink Audit { get; } = new();
    public ToolExecutor Executor { get; }
    public ToolExecutionContext Context { get; }

    public GitLsRemoteStubFixture()
    {
        Context = new(Guid.NewGuid(), Root, [Root], ApprovalPolicy.AskEveryTime, Timeout: TimeSpan.FromSeconds(5));
        var repository = new GitIndexRepositoryProbe { Authorize = (_, _) => Task.FromResult(
            new ToolGitRepository(Root, Path.Combine(Root, ".git"), Path.Combine(Root, ".git"))) };
        var git = new GitCommandRunner(Processes);
        var tool = new GitLsRemoteTool(new GitRemoteUrlReader(new AuthorizedPathResolver(), repository, git),
            new GitRemoteUrlTarget(new GitPushDestinationProbe()), git);
        Audit.Release.SetResult();
        Executor = new(new([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)), Audit, Clock);
    }

    public Task<ToolResult> ExecuteAsync(object arguments, CancellationToken token, ToolExecutionContext? context = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "git.ls_remote",
            JsonSerializer.SerializeToElement(arguments)), context ?? Context, token);

    public Task<ToolResult> ExecuteAsync(object arguments, ToolExecutionContext? context = null) =>
        ExecuteAsync(arguments, TestContext.Current.CancellationToken, context);

    public void Dispose()
    {
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase);
        Directory.Delete(Root, recursive: true);
    }
}
