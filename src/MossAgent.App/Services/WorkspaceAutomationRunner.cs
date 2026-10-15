using MossAgent.Application.Automations;
using MossAgent.Application.Persistence;
using MossAgent.App.ViewModels;
using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed class WorkspaceAutomationRunner(
    IWorkspaceRepository projects,
    IConfigurationRepository configurations,
    WorkspaceViewModel workspace) : IAutomationTaskRunner
{
    public async Task<AutomationRunStatus> RunAsync(AutomationSchedule schedule, Guid taskId,
        CancellationToken cancellationToken)
    {
        AutomationTiming.Validate(schedule);
        var project = (await projects.GetProjectsAsync(cancellationToken)).FirstOrDefault(item => item.Id == schedule.ProjectId)
            ?? throw new InvalidOperationException("Automation project unavailable.");
        var provider = (await configurations.GetProvidersAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == schedule.ProviderId && item.IsEnabled)
            ?? throw new InvalidOperationException("Automation provider unavailable.");
        var model = (await configurations.GetModelsAsync(provider.Id, cancellationToken))
            .FirstOrDefault(item => item.Id == schedule.ModelId && item.ProviderId == provider.Id && item.IsEnabled)
            ?? throw new InvalidOperationException("Automation model unavailable.");
        var task = await workspace.RunBackgroundAsync(project, provider, model, schedule, taskId, cancellationToken);
        return task.Status switch
        {
            AgentTaskStatus.Completed => AutomationRunStatus.Completed,
            AgentTaskStatus.Cancelled => AutomationRunStatus.Cancelled,
            AgentTaskStatus.Interrupted => AutomationRunStatus.Interrupted,
            _ => AutomationRunStatus.Failed
        };
    }
}
