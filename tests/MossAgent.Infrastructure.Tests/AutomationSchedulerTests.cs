using Microsoft.Data.Sqlite;
using MossAgent.Application.Automations;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationSchedulerTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly AutomationTestClock _clock = new();
    private readonly AutomationProbeRunner _runner = new();
    private AutomationScheduler _scheduler = null!;
    private SqliteAutomationRepository _repository = null!;
    private SqliteConnectionFactory _connections = null!;
    private CancellationToken Token => TestContext.Current.CancellationToken;
    private AutomationSchedule Schedule(bool enabled = true) => new(Guid.NewGuid(), "计划", "只读任务", Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), AutomationFrequency.Hourly, 1, 9, 0, DayOfWeek.Monday, "UTC", ApprovalPolicy.ReadOnly, 2, enabled,
        _clock.GetUtcNow().AddMinutes(1));

    public async ValueTask InitializeAsync()
    {
        var paths = new AppDataPaths(_root);
        _connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, _connections).InitializeAsync(Token);
        _repository = new SqliteAutomationRepository(_connections);
        _scheduler = new AutomationScheduler(_repository, _runner, new AutomationSchedulerLease(paths), _clock);
        await _scheduler.StartAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _scheduler.DisposeAsync();
        SqliteTestPool.Clear(_root);
        Directory.Delete(_root, true);
    }

    private async Task WaitFinishedAsync(Guid id)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (_scheduler.IsRunning(id) || (await _repository.GetRunsAsync(id, 50, timeout.Token)).Count == 0)
            await Task.Delay(10, timeout.Token);
    }

    [Fact]
    public async Task DuePlan_ClaimsOnceAndPersistsTheActualTaskIdentity()
    {
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _scheduler.TickAsync(Token);
        await WaitFinishedAsync(plan.Id);
        await _scheduler.TickAsync(Token);
        var request = Assert.Single(_runner.Requests);
        var run = Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token));
        Assert.Equal(request.TaskId, run.TaskId);
        Assert.Equal(ApprovalPolicy.ReadOnly, request.Schedule.ApprovalPolicy);
        Assert.False(run.IsManual);
        Assert.Equal(AutomationRunStatus.Completed, run.Status);
        Assert.Equal(_clock.GetUtcNow().AddHours(1), Assert.Single(await _repository.GetAllAsync(Token)).NextRunAt);
    }

    [Fact]
    public async Task MissedBeforeStartup_DoesNotCatchUp()
    {
        var plan = Schedule() with { NextRunAt = _clock.GetUtcNow().AddDays(-2) };
        await _repository.SaveAsync(plan, Token);
        await _scheduler.TickAsync(Token);
        Assert.Empty(_runner.Requests);
        Assert.Empty(await _repository.GetRunsAsync(plan.Id, 50, Token));
        Assert.Equal(_clock.GetUtcNow().AddHours(1), Assert.Single(await _repository.GetAllAsync(Token)).NextRunAt);
    }

    [Fact]
    public async Task PausedPlan_OnlyExplicitManualRunCanExecute()
    {
        var plan = Schedule(false);
        await _repository.SaveAsync(plan, Token);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _scheduler.TickAsync(Token);
        Assert.Empty(_runner.Requests);
        Assert.True(await _scheduler.RunNowAsync(plan.Id, Token));
        await WaitFinishedAsync(plan.Id);
        Assert.True(Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).IsManual);
        Assert.False(Assert.Single(await _repository.GetAllAsync(Token)).IsEnabled);
    }

    [Fact]
    public async Task RunningPlan_DoesNotOverlapAndStopPersistsCancellation()
    {
        _runner.Block = true;
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        Assert.True(await _scheduler.RunNowAsync(plan.Id, Token));
        await _runner.Started.Task.WaitAsync(Token);
        Assert.False(await _scheduler.RunNowAsync(plan.Id, Token));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _scheduler.TickAsync(Token);
        Assert.Single(_runner.Requests);
        _scheduler.StopRun(plan.Id);
        await WaitFinishedAsync(plan.Id);
        Assert.Equal(AutomationRunStatus.Cancelled, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).Status);
    }

    [Fact]
    public async Task Timeout_IsRecordedWithoutWaitingForRealTime()
    {
        _runner.Block = true;
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        await _scheduler.RunNowAsync(plan.Id, Token);
        await _runner.Started.Task.WaitAsync(Token);
        _clock.Advance(TimeSpan.FromMinutes(2));
        await WaitFinishedAsync(plan.Id);
        Assert.Equal(AutomationRunStatus.TimedOut, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).Status);
    }

    [Fact]
    public async Task SecondaryInstance_CannotRecoverOrTriggerOwnersRunningWork()
    {
        _runner.Block = true;
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        await _scheduler.RunNowAsync(plan.Id, Token);
        await using var secondary = new AutomationScheduler(_repository, new AutomationProbeRunner(),
            new AutomationSchedulerLease(new AppDataPaths(_root)), _clock);
        await secondary.StartAsync(Token);
        Assert.False(secondary.IsAvailable);
        Assert.Equal("another-instance", secondary.ErrorCode);
        Assert.False(await secondary.RunNowAsync(plan.Id, Token));
        Assert.Equal(AutomationRunStatus.Running, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).Status);
        _scheduler.StopRun(plan.Id);
        await WaitFinishedAsync(plan.Id);
    }

    [Fact]
    public async Task Shutdown_CancelsAndWaitsBeforeReleasingLease()
    {
        _runner.Block = true;
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        await _scheduler.RunNowAsync(plan.Id, Token);
        await _runner.Started.Task.WaitAsync(Token);
        await _scheduler.StopAsync();
        Assert.False(_scheduler.IsAvailable);
        Assert.False(await _scheduler.RunNowAsync(plan.Id, Token));
        Assert.Equal(AutomationRunStatus.Cancelled, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).Status);
        using var nextOwner = new AutomationSchedulerLease(new AppDataPaths(_root));
        Assert.True(nextOwner.TryAcquire());
    }

    [Fact]
    public async Task ExecutionFailure_IsSafeAndDoesNotPersistSecrets()
    {
        _runner.Throw = true;
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        await _scheduler.RunNowAsync(plan.Id, Token);
        await WaitFinishedAsync(plan.Id);
        var run = Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token));
        Assert.Equal(AutomationRunStatus.Failed, run.Status);
        Assert.Equal("execution-unavailable", run.ErrorCode);
    }

    [Fact]
    public async Task FinalizationFailure_LeavesDurableClaimToPreventAutomaticReplay()
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER reject_finish BEFORE UPDATE ON automation_runs BEGIN SELECT RAISE(ABORT,'synthetic'); END;";
        await command.ExecuteNonQueryAsync(Token);
        var plan = Schedule();
        await _repository.SaveAsync(plan, Token);
        await _scheduler.RunNowAsync(plan.Id, Token);
        await WaitFinishedAsync(plan.Id);
        Assert.Equal("persistence-error", _scheduler.ErrorCode);
        Assert.False(await _scheduler.RunNowAsync(plan.Id, Token));
        await _scheduler.TickAsync(Token);
        Assert.Equal("persistence-error", _scheduler.ErrorCode);
        Assert.Single(_runner.Requests);
        Assert.Equal(AutomationRunStatus.Running, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).Status);
    }
}
