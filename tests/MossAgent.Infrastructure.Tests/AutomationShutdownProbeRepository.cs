using System.Collections.Concurrent;
using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationShutdownProbeRepository : IAutomationRepository
{
    public AutomationSchedule Plan { get; set; } = new(Guid.NewGuid(), "shutdown fixture", "read only", Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday, "UTC",
        ApprovalPolicy.ReadOnly, 30, false, DateTimeOffset.UtcNow.AddDays(1));
    public string? BlockAt { get; set; }
    public bool IsCooperative { get; set; } = true;
    public bool RejectFinish { get; set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<AutomationRun> Finished { get; } = new();
    public AutomationRun? Claimed { get; private set; }
    public CancellationToken PortToken { get; private set; }
    public int Reads { get; private set; }
    public int Claims { get; private set; }
    public int FinishAttempts { get; private set; }
    public int Recoveries { get; private set; }

    private async Task WaitAsync(string phase, CancellationToken token)
    {
        if (BlockAt != phase) return;
        PortToken = token;
        using var registration = token.Register(() => Cancelled.TrySetResult());
        Entered.TrySetResult();
        try
        {
            if (IsCooperative) await Released.Task.WaitAsync(token);
            else await Released.Task;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { Cancelled.TrySetResult(); throw; }
    }

    public async Task<IReadOnlyList<AutomationSchedule>> GetAllAsync(CancellationToken cancellationToken)
    {
        Reads++;
        await WaitAsync("read", cancellationToken);
        return [Plan];
    }

    public async Task RecoverAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Recoveries++;
        await WaitAsync("recover", cancellationToken);
    }

    public async Task<AutomationRun?> TryClaimAsync(AutomationSchedule expected, DateTimeOffset now,
        DateTimeOffset nextRunAt, bool manual, CancellationToken cancellationToken)
    {
        Claims++;
        await WaitAsync("claim", cancellationToken);
        Claimed = new AutomationRun(Guid.NewGuid(), expected.Id, Guid.NewGuid(), now, AutomationRunStatus.Running, manual);
        return Claimed;
    }

    public async Task FinishAsync(AutomationRun run, CancellationToken cancellationToken)
    {
        FinishAttempts++;
        await WaitAsync("finish", cancellationToken);
        if (RejectFinish) throw new IOException("synthetic-secret-not-for-user-output");
        Finished.Enqueue(run);
    }

    public Task<bool> SaveAsync(AutomationSchedule schedule, CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<bool> ArchiveAsync(Guid id, long revision, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<AutomationRun>> GetRunsAsync(Guid scheduleId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AutomationRun>>(Finished.ToArray());
}
