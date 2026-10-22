using MossAgent.Application.Automations;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

/// <summary>
/// Plan-level authorization snapshot behaviour: a fixed plan stops when the project authorization
/// changes, an explicitly following plan keeps running, and legacy plans without a snapshot still
/// claim as before.
/// </summary>
public sealed class AutomationScopeClaimTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private SqliteAutomationRepository _automations = null!;
    private SqliteWorkspaceRepository _workspaces = null!;
    private ProjectProfile _project = null!;
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var paths = new AppDataPaths(_root);
        var connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(Token);
        _automations = new(connections);
        _workspaces = new(connections);
        _project = new ProjectProfile(Guid.NewGuid(), "scope", _root, [_root], DateTimeOffset.UtcNow);
        await _workspaces.SaveProjectAsync(_project, Token);
    }

    public ValueTask DisposeAsync()
    {
        SqliteTestPool.Clear(_root);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FixedSnapshotPlanStopsOnceAndRecordsAuthorizationChange()
    {
        await _automations.SaveAsync(Plan(AutomationScopePolicy.FixedSnapshot), Token);
        var plan = Assert.Single(await _automations.GetAllAsync(Token));
        await RevokeRootsAsync();
        var now = DateTimeOffset.UtcNow;

        Assert.Null(await _automations.TryClaimAsync(plan, now, now.AddHours(1), false, Token));

        var blocked = Assert.Single(await _automations.GetRunsAsync(plan.Id, 50, Token));
        Assert.Equal(AutomationRunStatus.Failed, blocked.Status);
        Assert.Equal("authorization-changed", blocked.ErrorCode);
        Assert.False(blocked.IsManual);
        Assert.Equal(now.AddHours(1), Assert.Single(await _automations.GetAllAsync(Token)).NextRunAt);

        var rescheduled = Assert.Single(await _automations.GetAllAsync(Token));
        Assert.Null(await _automations.TryClaimAsync(rescheduled, now.AddHours(2), now.AddHours(3), false, Token));
        Assert.Equal(2, (await _automations.GetRunsAsync(plan.Id, 50, Token)).Count);
    }

    [Fact]
    public async Task FixedSnapshotPlanClaimsWhileTheProjectAuthorizationIsUnchanged()
    {
        await _automations.SaveAsync(Plan(AutomationScopePolicy.FixedSnapshot), Token);
        var plan = Assert.Single(await _automations.GetAllAsync(Token));
        var now = DateTimeOffset.UtcNow;

        var run = Assert.IsType<AutomationRun>(await _automations.TryClaimAsync(plan, now, now.AddHours(1), true, Token));

        Assert.Equal(AutomationRunStatus.Running, run.Status);
        Assert.DoesNotContain(await _automations.GetRunsAsync(plan.Id, 50, Token), item => item.ErrorCode is not null);
    }

    [Fact]
    public async Task FollowProjectPlanKeepsClaimingAfterTheAuthorizationChanges()
    {
        await _automations.SaveAsync(Plan(AutomationScopePolicy.FollowProject), Token);
        var plan = Assert.Single(await _automations.GetAllAsync(Token));
        await RevokeRootsAsync();
        var now = DateTimeOffset.UtcNow;

        Assert.NotNull(await _automations.TryClaimAsync(plan, now, now.AddHours(1), true, Token));
    }

    [Fact]
    public async Task LegacyPlanWithoutSnapshotKeepsClaimingAndIsBoundedOnceResaved()
    {
        var legacy = Plan(AutomationScopePolicy.FixedSnapshot) with { Scope = null, Model = null };
        await _automations.SaveAsync(legacy, Token);
        var plan = Assert.Single(await _automations.GetAllAsync(Token));
        await RevokeRootsAsync();
        var now = DateTimeOffset.UtcNow;

        // A legacy plan has no saved revision, so it keeps running instead of blocking on every tick.
        var legacyRun = Assert.IsType<AutomationRun>(await _automations.TryClaimAsync(plan, now, now.AddHours(1), true, Token));
        await _automations.FinishAsync(legacyRun with
        { Status = AutomationRunStatus.Completed, FinishedAt = now }, Token);

        var current = Assert.Single(await _automations.GetAllAsync(Token));
        var project = Assert.Single(await _workspaces.GetProjectsAsync(Token));
        var resaved = current with
        {
            Scope = AutomationScopeRules.CreateScope(project, AutomationScopePolicy.FixedSnapshot),
            Model = AutomationScopeRules.CreateModel(Model())
        };
        Assert.True(await _automations.SaveAsync(resaved, Token));
        await GrantRootsAsync();

        var rebound = Assert.Single(await _automations.GetAllAsync(Token));
        Assert.Null(await _automations.TryClaimAsync(rebound, now.AddMinutes(1), now.AddHours(1), true, Token));
        Assert.Contains(await _automations.GetRunsAsync(plan.Id, 50, Token),
            item => item.ErrorCode == "authorization-changed");
    }

    [Fact]
    public void InvalidSnapshotFieldsAreRejectedBeforePersistence()
    {
        var plan = Plan(AutomationScopePolicy.FixedSnapshot);
        Assert.Throws<ArgumentException>(() => AutomationTiming.Validate(
            plan with { Scope = plan.Scope! with { ProjectRevision = 0 } }));
        Assert.Throws<ArgumentException>(() => AutomationTiming.Validate(
            plan with { Scope = plan.Scope! with { AuthorizedRoots = ["a", "a"] } }));
        Assert.Throws<ArgumentException>(() => AutomationTiming.Validate(
            plan with { Model = plan.Model! with { Temperature = 3 } }));
    }

    private AutomationSchedule Plan(AutomationScopePolicy policy) => new(Guid.NewGuid(), "审查", "只读检查。",
        _project.Id, Guid.NewGuid(), Guid.NewGuid(), AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
        TimeZoneInfo.Utc.Id, ApprovalPolicy.ReadOnly, 30, true, DateTimeOffset.UtcNow.AddHours(1),
        Scope: AutomationScopeRules.CreateScope(_project, policy), Model: AutomationScopeRules.CreateModel(Model()));

    private static ModelProfile Model() => new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "fixture", 8192, 1024,
        "medium", 0.2, 0.9, false, false, true, true, "{}");

    /// <summary>
    /// Simulates a project authorization change: the root set is reduced and the revision advances.
    /// </summary>
    private async Task RevokeRootsAsync() =>
        await _workspaces.SaveProjectAsync(_project with { AuthorizedDirectories = [] }, Token);

    /// <summary>
    /// Simulates a second project authorization change that adds the root back.
    /// </summary>
    private async Task GrantRootsAsync()
    {
        var project = Assert.Single(await _workspaces.GetProjectsAsync(Token));
        await _workspaces.SaveProjectAsync(project with { AuthorizedDirectories = [_root] }, Token);
    }
}
