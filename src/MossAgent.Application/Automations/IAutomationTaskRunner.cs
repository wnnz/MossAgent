using MossAgent.Domain;

namespace MossAgent.Application.Automations;

public interface IAutomationTaskRunner
{
    Task<AutomationRunStatus> RunAsync(AutomationSchedule schedule, Guid taskId,
        CancellationToken cancellationToken);
}
