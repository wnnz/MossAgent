using MossAgent.Application.Automations;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.App.ViewModels;
using MossAgent.Domain;

namespace MossAgent.App.Services;

/// <summary>
/// Runs one claimed plan with the authorization and model parameters saved with it.
/// The saved snapshot is re-checked here as well, so a change that happened after the claim cannot
/// run with different roots or parameters.
/// </summary>
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
        ValidateScope(schedule, project);
        var provider = (await configurations.GetProvidersAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == schedule.ProviderId && item.IsEnabled)
            ?? throw new InvalidOperationException("Automation provider unavailable.");
        var current = (await configurations.GetModelsAsync(provider.Id, cancellationToken))
            .FirstOrDefault(item => item.Id == schedule.ModelId && item.ProviderId == provider.Id && item.IsEnabled)
            ?? throw new InvalidOperationException("Automation model unavailable.");
        var model = AutomationScopeRules.ResolveModel(schedule, current);
        if (!ModelReasoningPolicy.Supports(provider.Protocol, model.ModelId, model.ReasoningEffort))
            throw new InvalidOperationException("Automation reasoning effort is unavailable.");
        var task = await workspace.RunBackgroundAsync(project, provider, model, schedule, taskId, cancellationToken);
        return task.Status switch
        {
            AgentTaskStatus.Completed => AutomationRunStatus.Completed,
            AgentTaskStatus.Cancelled => AutomationRunStatus.Cancelled,
            AgentTaskStatus.Interrupted => AutomationRunStatus.Interrupted,
            _ => AutomationRunStatus.Failed
        };
    }

    /// <summary>
    /// Rejects a plan whose fixed authorization snapshot no longer matches its project.
    /// </summary>
    /// <param name="schedule">Plan being executed.</param>
    /// <param name="project">Project row read at execution time.</param>
    /// <exception cref="AutomationScopeChangedException">Thrown when the saved snapshot no longer matches.</exception>
    private static void ValidateScope(AutomationSchedule schedule, ProjectProfile project)
    {
        if (AutomationScopeRules.PolicyOf(schedule) != AutomationScopePolicy.FixedSnapshot) return;
        if (schedule.Scope is { } scope && !AutomationScopeRules.Matches(project, scope))
            throw new AutomationScopeChangedException();
    }
}
