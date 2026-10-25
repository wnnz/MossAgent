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

internal sealed class GitRemoteWriteFixture : IDisposable
{
    private readonly GitIndexFixture _repository = new();
    private readonly string _root = Directory.CreateTempSubdirectory("MossAgent-remote-").FullName;
    public string Root => _repository.Root;
    public string Remote => Path.Combine(_root, "remote.git");
    public string OtherRemote => Path.Combine(_root, "other.git");
    public ToolExecutionContext Context { get; }
    public ToolExecutor Executor { get; }
    public ToolProgressAuditSink Audit { get; } = new();
    public bool Approved { get; set; } = true;
    public Func<Task>? OnApproval { get; set; }
    public int ApprovalRequests { get; private set; }

    public GitRemoteWriteFixture()
    {
        Context = _repository.Context with { AuthorizedRoots = [_repository.Root, _root], ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        var launcher = new FixtureTaskProcessLauncher();
        var runner = new GitProcessRunner(launcher);
        var authorization = new GitRepositoryAuthorization(runner);
        var git = new GitCommandRunner(launcher);
        var paths = new AuthorizedPathResolver();
        var tools = new ToolRegistry([new GitRemotesTool(new GitRemoteStateReader(paths, authorization, git)),
            new GitRemoteSetUrlTool(new GitRemoteUrlReader(paths, authorization, git),
                new GitRemoteUrlTarget(new GitPushDestinationAuthorization(authorization, runner)), git),
            new GitBranchUpstreamTool(new GitBranchUpstreamReader(paths, authorization, git), git)]);
        Audit.Release.SetResult();
        Executor = new(tools, new PolicyApprovalService(async (_, _, _) =>
        { ApprovalRequests++; if (OnApproval is not null) await OnApproval(); return Approved; }), Audit);
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        foreach (var path in new[] { Remote, OtherRemote }) await GitAsync("init", "--bare", path);
        await GitAsync("remote", "add", "origin", "https://old.invalid/repo.git");
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, ToolExecutionContext? context = null) => Executor.ExecuteAsync(
        new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)),
        context ?? Context, TestContext.Current.CancellationToken);

    public async Task<GitRemotesReport> ReadAsync()
    {
        var result = await ExecuteAsync("git.remotes", new { });
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitRemotesReport>(result.Content!, JsonSerializerOptions.Web)!;
    }

    public async Task<GitRemoteEntry> RemoteEntryAsync(string name = "origin") =>
        Assert.Single((await ReadAsync()).Remotes, remote => remote.Name == name);

    public Task<string> GitAsync(params string[] arguments) => _repository.GitAsync(arguments);

    public async Task<string> HeadBranchAsync() => (await GitAsync("symbolic-ref", "--short", "HEAD")).Trim();

    public void Dispose()
    {
        _repository.Dispose();
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
