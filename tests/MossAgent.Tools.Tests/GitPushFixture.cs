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

internal sealed class GitPushFixture : IDisposable
{
    private readonly GitIndexFixture _source = new();
    private readonly string _root = Directory.CreateTempSubdirectory("MossAgent-push-").FullName;
    private readonly GitCommandRunner _git = new(new FixtureTaskProcessLauncher());
    private readonly List<string> _links = [];
    public string Source => _source.Root;
    public string Remote => Path.Combine(_root, "remote.git");
    public string OtherRemote => Path.Combine(_root, "other.git");
    public ToolExecutionContext Context { get; }
    public ToolExecutor Executor { get; }
    public ToolProgressAuditSink Audit { get; } = new();
    public bool Approved { get; set; } = true;
    public Func<Task>? OnApproval { get; set; }

    public GitPushFixture()
    {
        Context = _source.Context with { AuthorizedRoots = [Source, _root], ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        var runner = new GitProcessRunner(new FixtureTaskProcessLauncher());
        var authorization = new GitRepositoryAuthorization(runner);
        var states = new GitPushStateReader(new AuthorizedPathResolver(), authorization, _git,
            new(new GitPushDestinationAuthorization(authorization, runner)));
        Audit.Release.SetResult();
        Executor = new(new([new GitPushPreviewTool(states), new GitPushTool(states, _git)]), new PolicyApprovalService(async (_, _, _) =>
        { if (OnApproval is not null) await OnApproval(); return Approved; }), Audit);
    }

    public async Task InitializeAsync()
    {
        await _source.InitializeAsync();
        foreach (var path in new[] { Remote, OtherRemote }) await SourceGitAsync("init", "--bare", path);
        await SourceGitAsync("remote", "add", "origin", Remote);
    }

    public Task<ToolResult> ExecuteAsync(string name, object arguments, ToolExecutionContext? context = null) => Executor.ExecuteAsync(
        new(Guid.NewGuid().ToString("N"), name, JsonSerializer.SerializeToElement(arguments)), context ?? Context, TestContext.Current.CancellationToken);

    public async Task<GitPushPreview> PreviewAsync(object? arguments = null)
    {
        var result = await ExecuteAsync("git.push_preview", arguments ?? new { });
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitPushPreview>(result.Content!, JsonSerializerOptions.Web)!;
    }

    public Task<ToolResult> PushAsync(GitPushPreview preview, ToolExecutionContext? context = null) => ExecuteAsync("git.push",
        new { remote = preview.Remote, targetBranch = preview.TargetBranch, expectedState = preview.State }, context);
    public Task<string> SourceGitAsync(params string[] arguments) => _source.GitAsync(arguments);
    public async Task<string> RemoteGitAsync(params string[] arguments)
    {
        var result = await _git.RunAsync(Remote, arguments, Context, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Summary);
        return result.Content ?? string.Empty;
    }

    public async Task JunctionAsync(string link, string target)
    {
        Assert.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, Path.GetFullPath(link), StringComparison.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows()) Directory.CreateSymbolicLink(link, target);
        else
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await using var process = new FixtureTaskProcessLauncher().Start(new("cmd.exe", _root, ["/c", "mklink", "/J", link, target]), deadline.Token);
            var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            Assert.True(process.ExitCode == 0, $"Fixture junction failed: {await stdout} {await stderr}");
        }
        _links.Add(link);
    }

    public void Dispose()
    {
        _source.Dispose();
        foreach (var link in _links.AsEnumerable().Reverse())
            if (new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link);
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
