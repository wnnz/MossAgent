using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspacePersistenceFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider? _services;

    public IWorkspaceRepository Repository => _services!.GetRequiredService<IWorkspaceRepository>();
    public WorkspaceTaskFactory Factory => _services!.GetRequiredService<WorkspaceTaskFactory>();
    public ProjectProfile Project { get; private set; } = null!;
    public AgentTask Task { get; private set; } = null!;
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public WorkspaceTaskPanelCoordinator Panels => _services!.GetRequiredService<WorkspaceTaskPanelCoordinator>();
    public IToolExecutor Tools => _services!.GetRequiredService<IToolExecutor>();
    public IToolExecutionAuditReader Audit => _services!.GetRequiredService<IToolExecutionAuditReader>();
    public WorkspaceViewModel Workspace => _services!.GetRequiredService<WorkspaceViewModel>();

    public WorkspaceRunController CreateController(IAgentRunner runner, WorkspaceRunTracker runs) =>
        new(new WorkspaceRunExecutor(Repository, runner, Factory), runs, Factory);

    public WorkspaceRunState CreateState()
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.example.test/"), "synthetic-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 8192, 1024,
            "off", null, null, false, false, true, true, "{}");
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        return new WorkspaceRunState(Task, Project, provider, model, [new("你", "initial prompt"), assistant], assistant);
    }

    public static async Task<WorkspacePersistenceFixture> CreateAsync()
    {
        var fixture = new WorkspacePersistenceFixture();
        try
        {
            Directory.CreateDirectory(fixture._root);
            fixture._services = AppComposition.CreateServices(fixture._root);
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            fixture.Project = new ProjectProfile(Guid.NewGuid(), "atomic fixture", fixture._root, [fixture._root], DateTimeOffset.UtcNow);
            await fixture.Repository.SaveProjectAsync(fixture.Project, fixture.Token);
            fixture.Project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token));
            Assert.Single(fixture.Project.RootAuthorizations);
            fixture.Task = await fixture.Factory.CreateAsync(fixture.Project, "initial prompt", ApprovalPolicy.ReadOnly, false, fixture.Token);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task RejectMessageAsync(string content)
    {
        await using var connection = _services!.GetRequiredService<SqliteConnectionFactory>().Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER reject_message BEFORE INSERT ON messages
            WHEN NEW.content='rejected prompt'
            BEGIN SELECT RAISE(ABORT, 'synthetic refusal'); END;
            """;
        Assert.Equal("rejected prompt", content);
        await command.ExecuteNonQueryAsync(Token);
    }

    public async Task RejectCompletionAsync()
    {
        await using var connection = _services!.GetRequiredService<SqliteConnectionFactory>().Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TRIGGER reject_completion BEFORE UPDATE OF status ON agent_tasks
            WHEN NEW.status={(int)AgentTaskStatus.Completed}
            BEGIN SELECT RAISE(ABORT, 'synthetic refusal'); END;
            """;
        await command.ExecuteNonQueryAsync(Token);
    }

    public async Task RejectToolMessageAsync()
    {
        await using var connection = _services!.GetRequiredService<SqliteConnectionFactory>().Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TRIGGER reject_tool BEFORE INSERT ON messages
            WHEN NEW.role={(int)MessageRole.Tool}
            BEGIN SELECT RAISE(ABORT, 'synthetic refusal'); END;
            """;
        await command.ExecuteNonQueryAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        SqliteTestPool.Clear(_root);
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); return; }
            catch (IOException) when (!cleanup.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task.Delay(30, cleanup.Token).ConfigureAwait(false);
            }
        }
    }
}
