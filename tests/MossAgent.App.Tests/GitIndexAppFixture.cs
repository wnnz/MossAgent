using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class GitIndexAppFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    public string DirectoryPath { get; }
    public ToolExecutionContext Context { get; private set; }
    public IToolExecutor Tools => _services.GetRequiredService<IToolExecutor>();
    public IToolExecutionAuditReader Audit => _services.GetRequiredService<IToolExecutionAuditReader>();
    public MossAgent.App.ViewModels.MainWindowViewModel Main => _services.GetRequiredService<MossAgent.App.ViewModels.MainWindowViewModel>();
    public AgentTask TaskProfile { get; private set; } = null!;
    public CancellationToken Token => _deadline.Token;

    private GitIndexAppFixture()
    {
        _deadline.CancelAfter(TimeSpan.FromSeconds(30));
        DirectoryPath = Directory.CreateDirectory(Path.Combine(_root, "repository")).FullName;
        _services = AppComposition.CreateServices(Path.Combine(_root, "data"));
        Context = new(Guid.NewGuid(), DirectoryPath, [DirectoryPath], ApprovalPolicy.FullAccess);
    }

    public static async Task<GitIndexAppFixture> CreateAsync()
    {
        await SandboxedGitSupport.RequireAsync();
        var fixture = new GitIndexAppFixture();
        try
        {
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            var repository = fixture._services.GetRequiredService<IWorkspaceRepository>();
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "Git index fixture", fixture.DirectoryPath, [fixture.DirectoryPath], now);
            await repository.SaveProjectAsync(project, fixture.Token);
            fixture.TaskProfile = new(fixture.Context.TaskId, project.Id, "Git index", ApprovalPolicy.FullAccess,
                AgentTaskStatus.Completed, now, now, null);
            await repository.SaveTaskAsync(fixture.TaskProfile, fixture.Token);
            foreach (var arguments in new string[][]
            {
                ["init", "--initial-branch=fixture-main"], ["config", "user.name", "MossAgent fixture"],
                ["config", "user.email", "fixture@example.invalid"], ["config", "core.autocrlf", "false"],
                ["config", "core.fsmonitor", "false"], ["config", "commit.gpgsign", "false"],
                ["config", "core.hooksPath", Path.Combine(fixture._root, "disabled-hooks")]
            }) await fixture.GitAsync(arguments);
            await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "tracked.txt"), "baseline\n", fixture.Token);
            await fixture.GitAsync("add", "--", "tracked.txt");
            await fixture.GitAsync("commit", "-m", "fixture baseline");
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task<string> CreatePushRemoteAsync()
    {
        var remote = Directory.CreateDirectory(Path.Combine(_root, "remote.git")).FullName;
        await GitAsync("init", "--bare", remote);
        await GitAsync("remote", "add", "origin", remote);
        Context = Context with { AuthorizedRoots = [DirectoryPath, remote] };
        var repository = _services.GetRequiredService<IWorkspaceRepository>();
        var project = Assert.Single(await repository.GetProjectsAsync(Token));
        await repository.SaveProjectAsync(project with { AuthorizedDirectories = Context.AuthorizedRoots }, Token);
        return remote;
    }

    public async Task<string> GitAsync(params string[] arguments)
    {
        var result = await _services.GetRequiredService<GitCommandRunner>().RunAsync(DirectoryPath, arguments, Context, Token);
        Assert.True(result.IsSuccess, result.Summary);
        return result.Content ?? string.Empty;
    }

    public async Task WaitAsync(Func<bool> predicate)
    {
        while (!predicate()) await Task.Delay(10, Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteTestPool.Clear(Path.Combine(_root, "data"));
        _deadline.Dispose();
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(_root).StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture cleanup escaped its temporary parent.");
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
