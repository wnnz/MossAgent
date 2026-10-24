using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Git;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeRecoveryItemViewModel : ObservableObject
{
    internal WorkspaceWorktreeRecoveryItemViewModel(WorktreeCreationIntent intent,
        Func<WorkspaceWorktreeRecoveryItemViewModel, Task> acknowledge, Func<bool> canMutate)
    {
        Intent = intent;
        AcknowledgeCommand = new AsyncRelayCommand(() => acknowledge(this), canMutate);
    }

    internal WorktreeCreationIntent Intent { get; }
    public string Target => Intent.Target;
    public string StageLabel => WorkspaceText.Get(Intent.Stage == WorktreeCreationIntentStage.Failed
        ? "WorktreeRecoveryStageFailed" : "WorktreeRecoveryStageOrphaned");
    public string Reason => Intent.Stage == WorktreeCreationIntentStage.Failed
        ? FailureReason(Intent.ErrorCode) : OrphanReason(Intent.ErrorCode);
    public string Summary => $"{StageLabel} · {Reason}";
    public string Detail => string.Format(WorkspaceText.Get("WorktreeRecoveryDetail"), Basis,
        Intent.CreatedAt.ToLocalTime().ToString("g"));
    public string AcknowledgeLabel => WorkspaceText.Get("WorktreeRecoveryAcknowledge");
    public IAsyncRelayCommand AcknowledgeCommand { get; }

    private string Basis => Intent.BaseReference
        ?? (Intent.BaseCommit is { Length: > 12 } commit ? commit[..12] : Intent.BaseCommit)
        ?? WorkspaceText.Get("WorktreeRecoveryBasisDefault");

    internal void NotifyCommands() => AcknowledgeCommand.NotifyCanExecuteChanged();

    private static string FailureReason(string? code) => code switch
    {
        "timeout" => WorkspaceText.Get("WorktreeRecoveryFailureTimeout"),
        "authorization" => WorkspaceText.Get("WorktreeRecoveryFailureAuthorization"),
        "reference" => WorkspaceText.Get("WorktreeRecoveryFailureReference"),
        "io" => WorkspaceText.Get("WorktreeRecoveryFailureIo"),
        "git" => WorkspaceText.Get("WorktreeRecoveryFailureGit"),
        "unknown" or null => WorkspaceText.Get("WorktreeRecoveryFailureUnknown"),
        { } other => string.Format(WorkspaceText.Get("WorktreeRecoveryFailureOther"), other),
    };

    private static string OrphanReason(string? code) => code switch
    {
        "missing" => WorkspaceText.Get("WorktreeRecoveryOrphanMissing"),
        "untracked" => WorkspaceText.Get("WorktreeRecoveryOrphanUntracked"),
        "pointer" => WorkspaceText.Get("WorktreeRecoveryOrphanPointer"),
        "unregistered" => WorkspaceText.Get("WorktreeRecoveryOrphanUnregistered"),
        "foreign" => WorkspaceText.Get("WorktreeRecoveryOrphanForeign"),
        "unlinked" => WorkspaceText.Get("WorktreeRecoveryOrphanUnlinked"),
        "retained" => WorkspaceText.Get("WorktreeRecoveryOrphanRetained"),
        "unverified" => WorkspaceText.Get("WorktreeRecoveryOrphanUnverified"),
        _ => WorkspaceText.Get("WorktreeRecoveryOrphanUnknown"),
    };
}
