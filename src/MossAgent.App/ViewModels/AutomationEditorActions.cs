using MossAgent.Application.Automations;
using MossAgent.Application.Persistence;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class AutomationEditorActions(IAutomationRepository repository, IWorkspaceRepository projects,
    EnabledModelCatalog models, AutomationPlanListViewModel list, AutomationEditorViewModel editor,
    AutomationOperationViewModel operation, TimeProvider clock, Action<string?> setError, Action<string?> setNotice)
{
    private AutomationSavedDraft? _pendingSave;

    public Task EditAsync(AutomationSchedule? schedule, CancellationToken token)
    {
        var context = editor.ContextVersion;
        return operation.RunAsync(async cancellation =>
        {
            setError(null); setNotice(null);
            var availableProjects = await projects.GetProjectsAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            var availableModels = await models.ReadAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!IsCurrent(context)) return;
            if (schedule is not null && (list.IsLoading || list.HasLoadError || !list.Items.Any(item => item.Schedule == schedule)))
                setError(AutomationText.Get("Conflict"));
            else editor.Open(schedule, availableProjects, availableModels);
        }, token, "LoadingEditor", error => { if (IsCurrent(context)) setError(error); }, classifyFailure: _ => "LoadError");
    }

    public async Task SaveAsync(CancellationToken token)
    {
        var context = editor.ContextVersion;
        await operation.RunAsync(async cancellation =>
        {
            setError(null); setNotice(null);
            if (!IsCurrent(context) || !editor.IsOpen) return;
            var schedule = editor.Build(clock.GetUtcNow());
            var revision = editor.DraftRevision;
            var availableProjects = await projects.GetProjectsAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            var availableModels = await models.ReadAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            var owner = availableProjects.FirstOrDefault(project => project.Id == schedule.ProjectId);
            var choice = availableModels.FirstOrDefault(model =>
                model.Provider.Id == schedule.ProviderId && model.Model.Id == schedule.ModelId);
            if (owner is null || choice is null)
                throw new ArgumentException("Automation selection is unavailable.");
            if (!IsCurrent(context)) return;
            var snapshot = AutomationAuthorizationSnapshot.Capture(owner, choice.Provider, choice.Model,
                schedule.Prompt, schedule.ApprovalPolicy,
                (editor.Original?.AuthorizationSnapshot?.Generation ?? 0) + 1, clock.GetUtcNow());
            schedule = schedule with { AuthorizationSnapshot = snapshot };
            var saved = await repository.SaveAsync(schedule, cancellation);
            if (!operation.IsAlive) return;
            if (saved)
            {
                _pendingSave = new AutomationSavedDraft(schedule, context, revision, cancellation);
                editor.RecordSaved(schedule with { Revision = schedule.Revision + 1 }, context);
            }
            else if (IsCurrent(context)) setError(AutomationText.Get("Conflict"));
        }, token, "SavingPlan", error => { if (IsCurrent(context)) setError(error); },
            () => list.ReloadAsync(requireFresh: true), ClassifySaveFailure);
        if (_pendingSave is { } pending && IsCurrent(pending.ContextVersion) && list.HasLoadError)
            setNotice(AutomationText.Get("SavedRefreshPending"));
    }

    public void HandleSnapshotChanged()
    {
        if (_pendingSave is not { } pending) return;
        _pendingSave = null;
        if (!IsCurrent(pending.ContextVersion) || !editor.IsOpen) return;
        var stored = list.Items.FirstOrDefault(item => item.Schedule.Id == pending.Schedule.Id)?.Schedule;
        if (stored is null || stored.Revision != pending.Schedule.Revision + 1
            || stored.NextRunAt.ToUnixTimeMilliseconds() != pending.Schedule.NextRunAt.ToUnixTimeMilliseconds()
            || stored != pending.Schedule with { Revision = stored.Revision, NextRunAt = stored.NextRunAt })
        { setError(AutomationText.Get("Conflict")); return; }
        var cancelled = pending.Cancellation.IsCancellationRequested;
        var closed = editor.AcceptSaved(stored, pending.ContextVersion, pending.DraftRevision, !cancelled);
        if (closed)
        {
            if (!editor.IsOpen && editor.ContextVersion == pending.ContextVersion + 1) setNotice(null);
            return;
        }
        if (!IsCurrent(pending.ContextVersion)) return;
        setNotice(AutomationText.Get(cancelled ? "CancelledWriteCompleted" : "PreviousDraftSaved"));
    }

    private bool IsCurrent(long context) => operation.IsAlive && editor.ContextVersion == context;
    private static string ClassifySaveFailure(Exception exception) => exception is ArgumentException
        or TimeZoneNotFoundException or InvalidTimeZoneException or OverflowException ? "Invalid" : "SaveError";
}
