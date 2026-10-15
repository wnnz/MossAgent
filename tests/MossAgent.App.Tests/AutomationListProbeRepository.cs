using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class AutomationListProbeRepository(IAutomationRepository inner) : IAutomationRepository
{
    public Func<CancellationToken, Task<IReadOnlyList<AutomationSchedule>>>? Read { get; set; }
    public Func<AutomationSchedule, CancellationToken, Task<bool>>? Write { get; set; }
    public Func<Guid, long, CancellationToken, Task<bool>>? Archive { get; set; }
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }
    public Task<IReadOnlyList<AutomationSchedule>> GetAllAsync(CancellationToken token)
    {
        ReadCount++;
        return Read?.Invoke(token) ?? inner.GetAllAsync(token);
    }
    public Task<bool> SaveAsync(AutomationSchedule schedule, CancellationToken token)
    {
        WriteCount++;
        return Write?.Invoke(schedule, token) ?? inner.SaveAsync(schedule, token);
    }
    public Task<bool> ArchiveAsync(Guid id, long revision, CancellationToken token)
    {
        WriteCount++;
        return Archive?.Invoke(id, revision, token) ?? inner.ArchiveAsync(id, revision, token);
    }
    public Task<IReadOnlyList<AutomationRun>> GetRunsAsync(Guid id, int limit, CancellationToken token) => inner.GetRunsAsync(id, limit, token);
    public Task<AutomationRun?> TryClaimAsync(AutomationSchedule expected, DateTimeOffset now, DateTimeOffset next,
        bool manual, CancellationToken token) => inner.TryClaimAsync(expected, now, next, manual, token);
    public Task FinishAsync(AutomationRun run, CancellationToken token) => inner.FinishAsync(run, token);
    public Task RecoverAsync(DateTimeOffset now, CancellationToken token) => inner.RecoverAsync(now, token);
}
