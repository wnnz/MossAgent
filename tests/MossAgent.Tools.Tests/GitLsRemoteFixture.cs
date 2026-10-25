using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class GitLsRemoteFixture : IDisposable
{
    private readonly GitIndexFixture _repository = new();
    private readonly string _root = Directory.CreateTempSubdirectory("MossAgent-ls-remote-").FullName;
    public string Root => _repository.Root;
    public string Remote => Path.Combine(_root, "remote.git");
    public string OtherRemote => Path.Combine(_root, "other.git");
    public FixtureTaskProcessLauncher Launcher { get; } = new();
    public ToolExecutionContext Context { get; }
    public ToolExecutor Executor { get; }
    public ToolProgressAuditSink Audit { get; } = new();
    public int ApprovalRequests { get; private set; }

    public GitLsRemoteFixture()
    {
        Context = _repository.Context with { AuthorizedRoots = [_repository.Root, _root], ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        var runner = new GitProcessRunner(Launcher);
        var authorization = new GitRepositoryAuthorization(runner);
        var git = new GitCommandRunner(Launcher);
        var paths = new AuthorizedPathResolver();
        var tools = new ToolRegistry([new GitLsRemoteTool(new GitRemoteUrlReader(paths, authorization, git),
            new GitRemoteUrlTarget(new GitPushDestinationAuthorization(authorization, runner)), git)]);
        Audit.Release.SetResult();
        Executor = new(tools, new PolicyApprovalService((_, _, _) =>
        { ApprovalRequests++; return ValueTask.FromResult(true); }), Audit);
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        foreach (var path in new[] { Remote, OtherRemote }) await GitAsync("init", "--bare", path);
        await GitAsync("remote", "add", "origin", Remote);
        await GitAsync("tag", "-a", "-m", "fixture tag", "v1");
        await GitAsync("push", "origin", "HEAD:refs/heads/main", "HEAD:refs/heads/topic", "refs/tags/v1");
    }

    public Task<ToolResult> ExecuteAsync(object arguments, ToolExecutionContext? context = null) => Executor.ExecuteAsync(
        new(Guid.NewGuid().ToString("N"), "git.ls_remote", JsonSerializer.SerializeToElement(arguments)),
        context ?? Context, TestContext.Current.CancellationToken);

    public async Task<GitLsRemoteReport> ProbeAsync(object arguments, ToolExecutionContext? context = null)
    {
        var result = await ExecuteAsync(arguments, context);
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitLsRemoteReport>(result.Content!, JsonSerializerOptions.Web)!;
    }

    public Task<string> GitAsync(params string[] arguments) => _repository.GitAsync(arguments);

    public async Task PushAsync(int branches)
    {
        var refspecs = Enumerable.Range(0, branches).Select(index => $"HEAD:refs/heads/b{index:00}").ToArray();
        await GitAsync(["push", "origin", .. refspecs]);
    }

    public void Dispose()
    {
        _repository.Dispose();
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
