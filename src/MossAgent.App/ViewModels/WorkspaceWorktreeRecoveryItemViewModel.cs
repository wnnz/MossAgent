using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeRecoveryItemViewModel : ObservableObject
{
    private readonly Func<WorkspaceWorktreeRecoveryItemViewModel, CancellationToken, Task> _acknowledge;
    private bool _confirmationPending;
    private bool _isBusy;
    private bool _hasError;

    public WorkspaceWorktreeRecoveryItemViewModel(WorktreeCreationIntent intent,
        Func<WorkspaceWorktreeRecoveryItemViewModel, CancellationToken, Task> acknowledge)
    {
        Intent = intent;
        _acknowledge = acknowledge;
        AcknowledgeCommand = new GuardedAsyncRelayCommand(AcknowledgeAsync, () => CanAcknowledge && !IsBusy);
    }

    public WorktreeCreationIntent Intent { get; }
    public IAsyncRelayCommand AcknowledgeCommand { get; }
    public bool CanAcknowledge => Intent.Status is WorktreeCreationStatus.Failed or WorktreeCreationStatus.Orphaned;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyStateChanged(); } }
    public bool HasError
    {
        get => _hasError;
        private set { if (SetProperty(ref _hasError, value)) OnPropertyChanged(nameof(Error)); }
    }
    public string TaskLabel => string.Format(SettingsText.Get("WorktreeRecoveryTask"), Intent.TaskId);
    public string StatusLabel => SettingsText.Get(Intent.Status switch
    {
        WorktreeCreationStatus.Preparing => "WorktreeRecoveryStatusPreparing",
        WorktreeCreationStatus.Claimed => "WorktreeRecoveryStatusClaimed",
        WorktreeCreationStatus.GitCreated => "WorktreeRecoveryStatusGitCreated",
        WorktreeCreationStatus.Failed => "WorktreeRecoveryStatusFailed",
        WorktreeCreationStatus.Orphaned => "WorktreeRecoveryStatusOrphaned",
        _ => "WorktreeRecoveryStatusUnknown"
    });
    public string TargetPath => Intent.TargetPath;
    public string ErrorCode => string.IsNullOrWhiteSpace(Intent.ErrorCode) ? string.Empty : Intent.ErrorCode;
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
