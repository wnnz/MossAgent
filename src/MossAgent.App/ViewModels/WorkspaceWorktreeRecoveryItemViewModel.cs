using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Git;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeRecoveryItemViewModel : ObservableObject
{
    private readonly Func<WorkspaceWorktreeRecoveryItemViewModel, Task>? _retryRemove;

    internal WorkspaceWorktreeRecoveryItemViewModel(WorktreeCreationIntent intent,
        Func<WorkspaceWorktreeRecoveryItemViewModel, Task> acknowledge, Func<bool> canMutate)
    {
        Intent = intent;
        AcknowledgeCommand = new AsyncRelayCommand(() => acknowledge(this), canMutate);
        RetryRemoveCommand = new AsyncRelayCommand(() => Task.CompletedTask, canMutate);
    }

    internal WorkspaceWorktreeRecoveryItemViewModel(WorktreeCleanupIntent intent,
        Func<WorkspaceWorktreeRecoveryItemViewModel, Task> acknowledge, Func<bool> canMutate,
        Func<WorkspaceWorktreeRecoveryItemViewModel, Task>? retryRemove)
    {
        CleanupIntent = intent;
        _retryRemove = retryRemove;
        AcknowledgeCommand = new AsyncRelayCommand(() => acknowledge(this), canMutate);
        RetryRemoveCommand = new AsyncRelayCommand(
            () => retryRemove?.Invoke(this) ?? Task.CompletedTask, canMutate);
    }

    internal WorktreeCreationIntent? Intent { get; }
    internal WorktreeCleanupIntent? CleanupIntent { get; }
    internal bool IsCleanupItem => CleanupIntent is not null;
    public Guid TaskId => Intent?.TaskId ?? CleanupIntent!.TaskId;
    public string Target => Intent?.Target ?? CleanupIntent!.Target;
    public string StageLabel => WorkspaceText.Get(IsCleanupItem
        ? IsFailed ? "WorktreeCleanupStageFailed" : "WorktreeCleanupStageOrphaned"
        : IsFailed ? "WorktreeRecoveryStageFailed" : "WorktreeRecoveryStageOrphaned");
    public string Reason => IsFailed
        ? (IsCleanupItem ? CleanupFailureReason(ErrorCode) : FailureReason(ErrorCode))
        : (IsCleanupItem ? CleanupOrphanReason(ErrorCode) : OrphanReason(ErrorCode));
    public string Summary => $"{StageLabel} · {Reason}";
    public string Detail => string.Format(
        WorkspaceText.Get(IsCleanupItem ? "WorktreeCleanupDetail" : "WorktreeRecoveryDetail"),
        IsCleanupItem ? CleanupIntent!.Repository : Basis, CreatedAt.ToLocalTime().ToString("g"));
    public string AcknowledgeLabel => WorkspaceText.Get("WorktreeRecoveryAcknowledge");
    public string RetryRemoveLabel => WorkspaceText.Get("WorktreeRecoveryRetryRemove");
    public bool CanRetryRemove => _retryRemove is not null && IsCleanupItem
        && CleanupIntent!.Stage is WorktreeCleanupIntentStage.Failed or WorktreeCleanupIntentStage.Orphaned
        && ErrorCode != "removed";
    public IAsyncRelayCommand AcknowledgeCommand { get; }
    public IAsyncRelayCommand RetryRemoveCommand { get; }

    private string? ErrorCode => Intent?.ErrorCode ?? CleanupIntent?.ErrorCode;
    private bool IsFailed => Intent?.Stage == WorktreeCreationIntentStage.Failed
        || CleanupIntent?.Stage == WorktreeCleanupIntentStage.Failed;
    private DateTimeOffset CreatedAt => Intent?.CreatedAt ?? CleanupIntent!.CreatedAt;
    private string Basis => Intent!.BaseReference
        ?? (Intent.BaseCommit is { Length: > 12 } commit ? commit[..12] : Intent.BaseCommit)
        ?? WorkspaceText.Get("WorktreeRecoveryBasisDefault");

    internal void NotifyCommands()
    {
        AcknowledgeCommand.NotifyCanExecuteChanged();
        RetryRemoveCommand.NotifyCanExecuteChanged();
    }

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

    private static string CleanupFailureReason(string? code) =>
        code == "git" ? WorkspaceText.Get("WorktreeCleanupFailureGit") : FailureReason(code);

    private static string CleanupOrphanReason(string? code) => code switch
    {
        "removed" => WorkspaceText.Get("WorktreeCleanupOrphanRemoved"),
        "retained" => WorkspaceText.Get("WorktreeCleanupOrphanRetained"),
        "unverified" => WorkspaceText.Get("WorktreeRecoveryOrphanUnverified"),
        _ => WorkspaceText.Get("WorktreeRecoveryOrphanUnknown"),
    };
}
