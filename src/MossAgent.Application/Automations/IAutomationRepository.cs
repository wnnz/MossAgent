using MossAgent.Domain;

namespace MossAgent.Application.Automations;

public interface IAutomationRepository
{
    Task<IReadOnlyList<AutomationSchedule>> GetAllAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(AutomationSchedule schedule, CancellationToken cancellationToken);
    Task<bool> ArchiveAsync(Guid id, long revision, CancellationToken cancellationToken);
    Task<IReadOnlyList<AutomationRun>> GetRunsAsync(Guid scheduleId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Claims one plan run inside a transaction.
    /// </summary>
    /// <param name="expected">Plan revision the caller read; a stale revision loses the claim.</param>
    /// <param name="now">Claim time.</param>
    /// <param name="nextRunAt">Next run time to persist for scheduled claims.</param>
    /// <param name="manual">True for an explicit manual run, which ignores the enabled/next-run gate.</param>
    /// <param name="cancellationToken">Claim cancellation token.</param>
    /// <returns>
    /// The claimed run, or null when the claim lost. A plan whose saved authorization no longer matches
    /// its project also returns null, after recording a failed run with <c>authorization-changed</c>.
    /// </returns>
    Task<AutomationRun?> TryClaimAsync(AutomationSchedule expected, DateTimeOffset now,
        DateTimeOffset nextRunAt, bool manual, CancellationToken cancellationToken);
    Task FinishAsync(AutomationRun run, CancellationToken cancellationToken);
    Task RecoverAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
