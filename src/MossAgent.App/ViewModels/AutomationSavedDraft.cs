using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed record AutomationSavedDraft(AutomationSchedule Schedule, long ContextVersion,
    long DraftRevision, CancellationToken Cancellation);
