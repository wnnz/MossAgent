using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeRecoveryItemViewModel : ObservableObject
{
    private readonly Func<WorkspaceWorktreeRecoveryItemViewModel, CancellationToken, Task> _acknowledge;
    private readonly WorktreeCreationIntent? _creationIntent;
    private readonly WorktreeCleanupIntent? _cleanupIntent;
    private bool _confirmationPending;
    private bool _isBusy;
    private bool _hasError;

    public WorkspaceWorktreeRecoveryItemViewModel(WorktreeCreationIntent intent,
        Func<WorkspaceWorktreeRecoveryItemViewModel, CancellationToken, Task> acknowledge)
        : this(acknowledge)
    {
        _creationIntent = intent;
        TaskId = intent.TaskId;
        TargetPath = intent.TargetPath;
        CanAcknowledge = intent.Status is WorktreeCreationStatus.Failed or WorktreeCreationStatus.Orphaned;
    }

    public WorkspaceWorktreeRecoveryItemViewModel(WorktreeCleanupIntent intent,
        bool canAcknowledge,
        Func<WorkspaceWorktreeRecoveryItemViewModel, CancellationToken, Task> acknowledge)
        : this(acknowledge)
    {
        _cleanupIntent = intent;
        TaskId = intent.TaskId;
        TargetPath = intent.TargetPath;
        QuarantinePath = intent.QuarantinePath;
        CanAcknowledge = canAcknowledge;
    }

    private WorkspaceWorktreeRecoveryItemViewModel(
        Func<WorkspaceWorktreeRecoveryItemViewModel, CancellationToken, Task> acknowledge)
    {
        _acknowledge = acknowledge;
        TargetPath = string.Empty;
        AcknowledgeCommand = new GuardedAsyncRelayCommand(
            AcknowledgeAsync, () => CanAcknowledge && !IsBusy);
    }

    public WorktreeCreationIntent? CreationIntent => _creationIntent;
    public WorktreeCleanupIntent? CleanupIntent => _cleanupIntent;
    public IAsyncRelayCommand AcknowledgeCommand { get; }
    public bool CanAcknowledge { get; }
    public Guid TaskId { get; }
    public string TargetPath { get; }
    public string QuarantinePath { get; } = string.Empty;
    public bool HasQuarantinePath => !string.IsNullOrWhiteSpace(QuarantinePath);
    public string QuarantineLabel => HasQuarantinePath
        ? string.Format(SettingsText.Get("WorktreeRecoveryQuarantine"), QuarantinePath)
        : string.Empty;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyStateChanged(); } }
    public bool HasError
    {
        get => _hasError;
        private set { if (SetProperty(ref _hasError, value)) OnPropertyChanged(nameof(Error)); }
    }
    public string TaskLabel => string.Format(SettingsText.Get("WorktreeRecoveryTask"), TaskId);
    public string StatusLabel => _creationIntent is { } creation
        ? SettingsText.Get(creation.Status switch
        {
            WorktreeCreationStatus.Preparing => "WorktreeRecoveryStatusPreparing",
            WorktreeCreationStatus.Claimed => "WorktreeRecoveryStatusClaimed",
            WorktreeCreationStatus.GitCreated => "WorktreeRecoveryStatusGitCreated",
            WorktreeCreationStatus.Failed => "WorktreeRecoveryStatusFailed",
            WorktreeCreationStatus.Orphaned => "WorktreeRecoveryStatusOrphaned",
            _ => "WorktreeRecoveryStatusUnknown"
        })
        : SettingsText.Get(CleanupStatusKey);
    private string CleanupStatusKey => _cleanupIntent switch
    {
        { State: WorktreeCleanupState.Failed } => "WorktreeRecoveryStatusCleanupFailed",
        { State: WorktreeCleanupState.Completed } =>
            "WorktreeRecoveryStatusCleanupTaskMetadataPending",
        { Stage: WorktreeCleanupStage.Preparing } => "WorktreeRecoveryStatusCleanupPreparing",
        { Stage: WorktreeCleanupStage.Quarantined } => "WorktreeRecoveryStatusCleanupQuarantined",
        { Stage: WorktreeCleanupStage.RegistrationDeleted } =>
            "WorktreeRecoveryStatusCleanupRegistrationDeleted",
        _ => "WorktreeRecoveryStatusUnknown"
    };
    public string ErrorCode => string.IsNullOrWhiteSpace(
        _creationIntent?.ErrorCode ?? _cleanupIntent?.ErrorCode) ? string.Empty : _creationIntent?.ErrorCode ?? _cleanupIntent?.ErrorCode ?? string.Empty;
    public bool HasErrorCode => !string.IsNullOrWhiteSpace(ErrorCode);
    public string Error => HasError ? SettingsText.Get("WorktreeRecoveryAcknowledgeFailed") : string.Empty;
    public string AcknowledgeLabel => SettingsText.Get(_confirmationPending
        ? "WorktreeRecoveryConfirmAcknowledged" : "WorktreeRecoveryAcknowledged");

    private async Task AcknowledgeAsync(CancellationToken cancellationToken)
    {
        if (!CanAcknowledge || IsBusy) return;
        if (!_confirmationPending)
        {
            _confirmationPending = true;
            OnPropertyChanged(nameof(AcknowledgeLabel));
            return;
        }
        _confirmationPending = false;
        IsBusy = true;
        try { await _acknowledge(this, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch { HasError = true; }
        finally { IsBusy = false; }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(AcknowledgeLabel));
        OnPropertyChanged(nameof(Error));
        AcknowledgeCommand.NotifyCanExecuteChanged();
    }
}
