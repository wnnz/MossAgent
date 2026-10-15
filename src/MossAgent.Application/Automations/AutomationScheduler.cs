using System.Collections.Concurrent;
using MossAgent.Domain;

namespace MossAgent.Application.Automations;

public sealed class AutomationScheduler(
    IAutomationRepository repository,
    IAutomationTaskRunner runner,
    IAutomationLease lease,
    TimeProvider clock) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AutomationSchedulerLifetime _lifetime = new();
    private readonly ConcurrentDictionary<Guid, AutomationExecution> _executions = [];
    private Task? _loop;
    private DateTimeOffset _startedAt;

    public event Action? StateChanged;
    public bool IsAvailable { get; private set; }
    public string? ErrorCode { get; private set; }
    public bool IsRunning(Guid scheduleId) => _executions.ContainsKey(scheduleId);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var operation = _lifetime.TryEnter(cancellationToken);
        if (operation is not null) await operation.RunAsync(StartCoreAsync).ConfigureAwait(false);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var acquired = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_loop is not null || IsAvailable) return;
            if (!(acquired = lease.TryAcquire())) { ErrorCode = "another-instance"; return; }
            _startedAt = clock.GetUtcNow();
            await repository.RecoverAsync(_startedAt, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            IsAvailable = true;
            ErrorCode = null;
            _loop = Task.Run(PollAsync);
        }
        catch (OperationCanceledException) { if (acquired) lease.Release(); throw; }
        catch (Exception) { ErrorCode = "scheduler-unavailable"; if (acquired) lease.Release(); }
        finally { _gate.Release(); StateChanged?.Invoke(); }
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        using var operation = _lifetime.TryEnter(cancellationToken);
        if (operation is not null) await operation.RunAsync(TickCoreAsync).ConfigureAwait(false);
    }

    private async Task TickCoreAsync(CancellationToken cancellationToken)
    {
        var changed = false;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAvailable) return;
            var now = clock.GetUtcNow();
            var schedules = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            foreach (var schedule in schedules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!schedule.IsEnabled || schedule.NextRunAt > now || IsRunning(schedule.Id)) continue;
                var next = AutomationTiming.Next(schedule, now);
                if (schedule.NextRunAt <= _startedAt)
                    changed |= await repository.SaveAsync(schedule with { NextRunAt = next }, cancellationToken).ConfigureAwait(false);
                else
                    changed |= await ClaimAsync(schedule, now, next, false, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (ErrorCode is not null and not "persistence-error") { ErrorCode = null; changed = true; }
        }
        finally { _gate.Release(); if (changed) StateChanged?.Invoke(); }
    }

    public async Task<bool> RunNowAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        using var operation = _lifetime.TryEnter(cancellationToken);
        return operation is not null && await operation.RunAsync(token => RunNowCoreAsync(scheduleId, token)).ConfigureAwait(false);
    }

    private async Task<bool> RunNowCoreAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAvailable || IsRunning(scheduleId)) return false;
            var schedules = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var schedule = schedules.FirstOrDefault(item => item.Id == scheduleId);
            if (schedule is null) return false;
            return await ClaimAsync(schedule, clock.GetUtcNow(), schedule.NextRunAt, true, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); StateChanged?.Invoke(); }
    }

    public void StopRun(Guid scheduleId)
    {
        if (_executions.TryGetValue(scheduleId, out var execution))
        {
            try { execution.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task<bool> ClaimAsync(AutomationSchedule schedule, DateTimeOffset now,
        DateTimeOffset next, bool manual, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var run = await repository.TryClaimAsync(schedule, now, next, manual, cancellationToken).ConfigureAwait(false);
        if (run is null) return false;
        if (cancellationToken.IsCancellationRequested)
        {
            await FinishAsync(run with { Status = AutomationRunStatus.Cancelled, FinishedAt = clock.GetUtcNow() }).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        var execution = new AutomationExecution(schedule.TimeoutMinutes, clock, _lifetime.Token);
        _executions[schedule.Id] = execution;
        execution.Completion = Task.Run(() => ExecuteAsync(schedule, run, execution));
        return true;
    }

    private async Task ExecuteAsync(AutomationSchedule schedule, AutomationRun run, AutomationExecution execution)
    {
        var status = AutomationRunStatus.Failed;
        string? error = null;
        try
        {
            execution.Token.ThrowIfCancellationRequested();
            status = await runner.RunAsync(schedule, run.TaskId, execution.Token).ConfigureAwait(false);
            if (status == AutomationRunStatus.Running) { status = AutomationRunStatus.Failed; error = "invalid-run-result"; }
            if (status == AutomationRunStatus.Cancelled && execution.IsTimedOut) status = AutomationRunStatus.TimedOut;
        }
        catch (OperationCanceledException) when (execution.Token.IsCancellationRequested)
        {
            status = execution.IsTimedOut ? AutomationRunStatus.TimedOut : AutomationRunStatus.Cancelled;
        }
        catch (Exception) { error = "execution-unavailable"; }
        try { await FinishAsync(run with { Status = status, FinishedAt = clock.GetUtcNow(), ErrorCode = error }).ConfigureAwait(false); }
        finally
        {
            _executions.TryRemove(schedule.Id, out _);
            execution.Dispose();
            StateChanged?.Invoke();
        }
    }

    private async Task FinishAsync(AutomationRun run)
    {
        try { await repository.FinishAsync(run, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception) { ErrorCode = "persistence-error"; }
    }

    private async Task PollAsync()
    {
        try
        {
            while (!_lifetime.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), clock, _lifetime.Token).ConfigureAwait(false);
                try { await TickAsync(_lifetime.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (_lifetime.Token.IsCancellationRequested) { return; }
                catch (Exception) { ErrorCode = "scheduler-unavailable"; StateChanged?.Invoke(); }
            }
        }
        catch (OperationCanceledException) when (_lifetime.Token.IsCancellationRequested) { }
    }

    public Task StopAsync()
    {
        IsAvailable = false;
        return _lifetime.StopAsync(StopCoreAsync);
    }

    private async Task StopCoreAsync()
    {
        IsAvailable = false;
        try
        {
            var work = _executions.Values.Select(item => item.Completion).ToList();
            if (_loop is not null) work.Add(_loop);
            await Task.WhenAll(work).ConfigureAwait(false);
        }
        finally { lease.Release(); _gate.Dispose(); StateChanged?.Invoke(); }
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
