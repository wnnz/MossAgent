using MossAgent.Domain;

namespace MossAgent.Application.Automations;

public interface IAutomationRepository
{
    Task<IReadOnlyList<AutomationSchedule>> GetAllAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(AutomationSchedule schedule, CancellationToken cancellationToken);
    Task<bool> ArchiveAsync(Guid id, long revision, CancellationToken cancellationToken);
    Task<IReadOnlyList<AutomationRun>> GetRunsAsync(Guid scheduleId, int limit, CancellationToken cancellationToken);
    Task<AutomationRun?> TryClaimAsync(AutomationSchedule expected, DateTimeOffset now,
        DateTimeOffset nextRunAt, bool manual, CancellationToken cancellationToken);
    Task FinishAsync(AutomationRun run, CancellationToken cancellationToken);
    Task RecoverAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
