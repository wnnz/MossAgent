using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class AutomationHistoryProbeRepository : IAutomationRepository
{
    public Func<Guid, CancellationToken, Task<IReadOnlyList<AutomationRun>>> Read { get; set; } = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([]);
    public List<(Guid ScheduleId, int Limit, CancellationToken Token)> Reads { get; } = [];
    public Task<IReadOnlyList<AutomationRun>> GetRunsAsync(Guid scheduleId, int limit, CancellationToken cancellationToken)
    {
        Reads.Add((scheduleId, limit, cancellationToken));
        return Read(scheduleId, cancellationToken);
    }
    public Task<IReadOnlyList<AutomationSchedule>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> SaveAsync(AutomationSchedule schedule, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> ArchiveAsync(Guid id, long revision, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AutomationRun?> TryClaimAsync(AutomationSchedule expected, DateTimeOffset now, DateTimeOffset nextRunAt,
        bool manual, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task FinishAsync(AutomationRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RecoverAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
}
