namespace MossAgent.Application.Automations;

public interface IAutomationTaskRunner
{
    Task<AutomationRunOutcome> RunAsync(AutomationRunContext context, Guid taskId,
        CancellationToken cancellationToken);
}
