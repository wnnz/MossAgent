using Microsoft.Data.Sqlite;
using MossAgent.Application.Automations;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationAuthorizationGateTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly AutomationTestClock _clock = new();
    private readonly AutomationProbeRunner _runner = new();
    private SqliteAutomationRepository _repository = null!;
    private SqliteWorkspaceRepository _workspace = null!;
    private SqliteConfigurationRepository _configurations = null!;
    private AutomationScheduler _scheduler = null!;
    private ProjectProfile _project = null!;
    private AiProvider _provider = null!;
    private ModelProfile _model = null!;
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var paths = new AppDataPaths(_root);
        var connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(Token);
        _repository = new SqliteAutomationRepository(connections);
        _workspace = new SqliteWorkspaceRepository(connections);
        _configurations = new SqliteConfigurationRepository(connections);
        _project = new ProjectProfile(Guid.NewGuid(), "gated", _root, [_root], DateTimeOffset.UtcNow);
        await _workspace.SaveProjectAsync(_project, Token);
        _project = Assert.Single(await _workspace.GetProjectsAsync(Token));
        _provider = new AiProvider(Guid.NewGuid(), "gated-provider", ProviderProtocol.OpenAiResponses,
            new Uri("http://localhost"), "key", null, false, true, new Dictionary<string, string>());
        await _configurations.SaveProviderAsync(_provider, Token);
        _model = new ModelProfile(Guid.NewGuid(), _provider.Id, "gated-model", "Gated", 32000,
            1024, "medium", null, null, false, false, false, true, "{}");
        await _configurations.SaveModelAsync(_model, Token);
        _scheduler = new AutomationScheduler(_repository, _runner, new AutomationSchedulerLease(paths),
            _clock, new AutomationAuthorizationGate(_workspace, _configurations));
        await _scheduler.StartAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _scheduler.DisposeAsync();
        SqliteTestPool.Clear(_root);
        Directory.Delete(_root, true);
    }

    private AutomationAuthorizationSnapshot Snapshot(ApprovalPolicy policy = ApprovalPolicy.ReadOnly,
        string prompt = "read only", IReadOnlyList<string>? directories = null) =>
        new(1, _project.Id, _project.AuthorizationRevision, directories ?? _project.AuthorizedDirectories,
            _provider.Id, _provider.Name, _model.Id, "Gated", policy, prompt, _clock.GetUtcNow());

    private AutomationSchedule Plan(AutomationAuthorizationSnapshot? snapshot, Guid? projectId = null,
        Guid? providerId = null) => new(Guid.NewGuid(), "gated plan", "read only", projectId ?? _project.Id,
        providerId ?? _provider.Id, _model.Id, AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
        "UTC", ApprovalPolicy.ReadOnly, 5, false, _clock.GetUtcNow().AddDays(1))
    { AuthorizationSnapshot = snapshot };

    private async Task<AutomationRun> FireAsync(AutomationSchedule plan)
    {
        Assert.True(await _scheduler.RunNowAsync(plan.Id, Token));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (_scheduler.IsRunning(plan.Id) || (await _repository.GetRunsAsync(plan.Id, 50, timeout.Token)).Count == 0)
            await Task.Delay(10, timeout.Token);
        return Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token));
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnectionFactory(new AppDataPaths(_root)).Create();
        await connection.OpenAsync(Token);
        return connection;
    }

    [Fact]
    public async Task MatchingSnapshot_RunsAgainstPinnedPolicyAndPrompt()
    {
        var plan = Plan(Snapshot(ApprovalPolicy.FullAccess, "pinned prompt")) with
        { ApprovalPolicy = ApprovalPolicy.ReadOnly, Prompt = "tampered prompt" };
        Assert.True(await _repository.SaveAsync(plan, Token));
        var run = await FireAsync(plan);
        Assert.Equal(AutomationRunStatus.Completed, run.Status);
        Assert.Null(run.ErrorCode);
        var request = Assert.Single(_runner.Requests);
        Assert.Equal(ApprovalPolicy.FullAccess, request.Schedule.ApprovalPolicy);
        Assert.Equal("pinned prompt", request.Schedule.Prompt);
    }

    [Fact]
    public async Task RevisionMismatch_BlocksWithReauthorizationReason()
    {
        var plan = Plan(Snapshot());
        Assert.True(await _repository.SaveAsync(plan, Token));
        await using var connection = await OpenAsync();
        await using var bump = connection.CreateCommand();
        bump.CommandText = "UPDATE projects SET authorization_revision=authorization_revision+1 WHERE id=$id;";
        bump.Parameters.AddWithValue("$id", _project.Id.ToString());
        await bump.ExecuteNonQueryAsync(Token);
        var run = await FireAsync(plan);
        Assert.Equal(AutomationRunStatus.Blocked, run.Status);
        Assert.Equal("authorization-revision-changed", run.ErrorCode);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task DisabledProvider_BlocksWithoutRunning()
    {
        var plan = Plan(Snapshot());
        Assert.True(await _repository.SaveAsync(plan, Token));
        await _configurations.SaveProviderAsync(_provider with { IsEnabled = false }, Token);
        var run = await FireAsync(plan);
        Assert.Equal(AutomationRunStatus.Blocked, run.Status);
        Assert.Equal("provider-unavailable", run.ErrorCode);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task ReducedDirectories_BlockWithoutRunning()
    {
        var escaped = Path.Combine(_root, "escaped");
        var plan = Plan(Snapshot(directories: [_root, escaped]));
        Assert.True(await _repository.SaveAsync(plan, Token));
        var run = await FireAsync(plan);
        Assert.Equal(AutomationRunStatus.Blocked, run.Status);
        Assert.Equal("authorization-directories-reduced", run.ErrorCode);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task MissingSnapshot_BlocksWithoutRunning()
    {
        var plan = Plan(null);
        Assert.True(await _repository.SaveAsync(plan, Token));
        var run = await FireAsync(plan);
        Assert.Equal(AutomationRunStatus.Blocked, run.Status);
        Assert.Equal("snapshot-missing", run.ErrorCode);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task MissingProject_BlocksWithoutRunning()
    {
        var ghost = new ProjectProfile(Guid.NewGuid(), "ghost", _root, [_root], DateTimeOffset.UtcNow);
        var snapshot = new AutomationAuthorizationSnapshot(1, ghost.Id, 1, [_root],
            _provider.Id, _provider.Name, _model.Id, "Gated", ApprovalPolicy.ReadOnly, "read only", _clock.GetUtcNow());
        var plan = Plan(snapshot, projectId: ghost.Id);
        Assert.True(await _repository.SaveAsync(plan, Token));
        var run = await FireAsync(plan);
        Assert.Equal(AutomationRunStatus.Blocked, run.Status);
        Assert.Equal("project-unavailable", run.ErrorCode);
        Assert.Empty(_runner.Requests);
    }
}
