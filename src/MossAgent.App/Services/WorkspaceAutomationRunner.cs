using MossAgent.Application.Automations;
using MossAgent.App.ViewModels;
using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed class WorkspaceAutomationRunner(WorkspaceViewModel workspace) : IAutomationTaskRunner
{
    public async Task<AutomationRunOutcome> RunAsync(AutomationRunContext context, Guid taskId,
        CancellationToken cancellationToken)
    {
        AutomationTiming.Validate(context.Schedule);
        var task = await workspace.RunBackgroundAsync(context.Project, context.Provider,
            context.Model, context.Schedule, taskId, cancellationToken);
        return new(task.Status switch
        {
            AgentTaskStatus.Completed => AutomationRunStatus.Completed,
            AgentTaskStatus.Cancelled => AutomationRunStatus.Cancelled,
            AgentTaskStatus.Interrupted => AutomationRunStatus.Interrupted,
            _ => AutomationRunStatus.Failed
        });
    }
}
