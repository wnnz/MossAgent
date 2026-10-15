using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteAutomationRepositoryTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private SqliteAutomationRepository _repository = null!;
    private SqliteDatabaseInitializer _initializer = null!;
    private CancellationToken Token => TestContext.Current.CancellationToken;
    private static AutomationSchedule Schedule() => new(Guid.NewGuid(), "代码审查", "只读检查项目。", Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
        TimeZoneInfo.Utc.Id, ApprovalPolicy.ReadOnly, 30, true, DateTimeOffset.UtcNow.AddMinutes(-1));

    public async ValueTask InitializeAsync()
    {
        var paths = new AppDataPaths(_root);
        var connections = new SqliteConnectionFactory(paths);
        _initializer = new SqliteDatabaseInitializer(paths, connections);
        await _initializer.InitializeAsync(Token);
        _repository = new SqliteAutomationRepository(connections);
    }

    public ValueTask DisposeAsync()
    {
        SqliteTestPool.Clear(_root);
        Directory.Delete(_root, true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ForwardMigration_RoundTripsAndDoesNotResetPlans()
    {
        var original = Schedule();
        Assert.True(await _repository.SaveAsync(original, Token));
        await _initializer.InitializeAsync(Token);
        var loaded = Assert.Single(await _repository.GetAllAsync(Token));
        Assert.Equal(original with { Revision = 1 }, loaded);
        Assert.False(await _repository.SaveAsync(original with { Name = "duplicate" }, Token));
        Assert.True(await _repository.SaveAsync(loaded with { IsEnabled = false }, Token));
        Assert.False(Assert.Single(await _repository.GetAllAsync(Token)).IsEnabled);
    }

    [Fact]
    public async Task StaleSaveAndArchive_CannotOverwriteNewerRevision()
    {
        await _repository.SaveAsync(Schedule(), Token);
        var original = Assert.Single(await _repository.GetAllAsync(Token));
        Assert.True(await _repository.SaveAsync(original with { Name = "新名称" }, Token));
        Assert.False(await _repository.SaveAsync(original with { Prompt = "旧编辑" }, Token));
        Assert.False(await _repository.ArchiveAsync(original.Id, original.Revision, Token));
        Assert.Equal("新名称", Assert.Single(await _repository.GetAllAsync(Token)).Name);
    }

    [Fact]
    public async Task ConcurrentClaims_RunOnceAndRejectStaleAndOverlappingStarts()
    {
        await _repository.SaveAsync(Schedule(), Token);
        var plan = Assert.Single(await _repository.GetAllAsync(Token));
        var now = DateTimeOffset.UtcNow;
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            _repository.TryClaimAsync(plan, now, now.AddDays(1), false, Token), Token)));
        var run = Assert.Single(claims.OfType<AutomationRun>());
        var current = Assert.Single(await _repository.GetAllAsync(Token));
        Assert.Equal(2, current.Revision);
        Assert.Null(await _repository.TryClaimAsync(current, now, now.AddDays(1), true, Token));
        Assert.False(await _repository.ArchiveAsync(current.Id, current.Revision, Token));
        Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token));
        await _repository.FinishAsync(run with { Status = AutomationRunStatus.Completed, FinishedAt = now }, Token);
        Assert.True(await _repository.ArchiveAsync(current.Id, current.Revision, Token));
        Assert.Empty(await _repository.GetAllAsync(Token));
        Assert.Equal(AutomationRunStatus.Completed, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)).Status);
    }

    [Fact]
    public async Task PausedAndFuturePlans_RequireExplicitManualRun()
    {
        await _repository.SaveAsync(Schedule() with { IsEnabled = false, NextRunAt = DateTimeOffset.UtcNow.AddDays(1) }, Token);
        var plan = Assert.Single(await _repository.GetAllAsync(Token));
        var now = DateTimeOffset.UtcNow;
        Assert.Null(await _repository.TryClaimAsync(plan, now, now.AddDays(1), false, Token));
        Assert.NotNull(await _repository.TryClaimAsync(plan, now, now.AddDays(1), true, Token));
        Assert.Equal(plan.NextRunAt, Assert.Single(await _repository.GetAllAsync(Token)).NextRunAt);
    }

    [Fact]
    public async Task Recovery_InterruptsWithoutReplayingOrReinterpretingHistory()
    {
        await _repository.SaveAsync(Schedule(), Token);
        var plan = Assert.Single(await _repository.GetAllAsync(Token));
        var now = DateTimeOffset.UtcNow;
        var run = await _repository.TryClaimAsync(plan, now, now.AddDays(1), true, Token);
        await _repository.RecoverAsync(now, Token);
        var recovered = Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token));
        Assert.Equal(AutomationRunStatus.Interrupted, recovered.Status);
        await _repository.FinishAsync(run! with { Status = AutomationRunStatus.Completed, FinishedAt = now }, Token);
        Assert.Equal(recovered, Assert.Single(await _repository.GetRunsAsync(plan.Id, 50, Token)));
    }

    [Fact]
    public async Task InvalidInputAndUnboundedHistory_AreRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.SaveAsync(Schedule() with { TimeoutMinutes = 0 }, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _repository.GetRunsAsync(Guid.NewGuid(), 201, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _repository.GetRunsAsync(Guid.NewGuid(), 0, Token));
    }

    [Fact]
    public async Task Lease_IsExclusiveAndReleasedOnDispose()
    {
        using var first = new AutomationSchedulerLease(new AppDataPaths(_root));
        using var second = new AutomationSchedulerLease(new AppDataPaths(_root));
        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
        first.Release();
        Assert.True(second.TryAcquire());
        await Task.CompletedTask;
    }
}
