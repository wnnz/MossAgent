using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Application.Automations;

/// <summary>Resolves live project/provider/model state once per claimed run and compares it
/// with the authorization snapshot pinned when the automation was saved. On success the
/// returned context pins the snapshot's approval policy and prompt.</summary>
public sealed class AutomationAuthorizationGate(IWorkspaceRepository projects,
    IConfigurationRepository configurations) : IAutomationAuthorizationGate
{
    public async Task<AutomationAuthorizationDecision> EvaluateAsync(AutomationSchedule schedule,
        CancellationToken cancellationToken)
    {
        var snapshot = schedule.AuthorizationSnapshot;
        if (snapshot is null) return new(null, AutomationSnapshotReview.MissingSnapshot);
        var project = (await projects.GetProjectsAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => item.Id == schedule.ProjectId);
        var provider = (await configurations.GetProvidersAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => item.Id == snapshot.ProviderId && item.IsEnabled);
        var model = provider is null ? null
            : (await configurations.GetModelsAsync(provider.Id, cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(item => item.Id == snapshot.ModelId && item.ProviderId == provider.Id && item.IsEnabled);
        var code = AutomationSnapshotReview.Evaluate(schedule, project, provider, model);
        // Pinned mode (default): the run executes with the snapshot's directory set, which the
        // review already proved to be a subset of the project's live authorization. Follow mode
        // is the explicit plan opt-in that keeps using the project's current roots instead.
        return code is not null ? new(null, code) : new(new AutomationRunContext(
            schedule with { ApprovalPolicy = snapshot.ApprovalPolicy, Prompt = snapshot.Prompt },
            project!, provider!, model!,
            schedule.FollowAuthorizationChanges ? null : snapshot.AuthorizedDirectories), null);
    }
}
