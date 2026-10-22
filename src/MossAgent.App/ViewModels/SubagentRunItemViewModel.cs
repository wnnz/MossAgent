using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class SubagentRunItemViewModel : ObservableObject
{
    private readonly SubagentRun _run;
    private bool _isConfirmationPending;

    public SubagentRunItemViewModel(SubagentRun run, string currentInstanceId,
        IRelayCommand<SubagentRunItemViewModel>? acknowledgeCommand = null)
    {
        _run = run;
        AcknowledgeCommand = acknowledgeCommand;
        NeedsRecovery = Status == SubagentRunStatus.Running
            && !run.OwnerInstanceId.Equals(currentInstanceId, StringComparison.Ordinal);
    }

    public Guid Id => _run.Id;
    public Guid TaskId => _run.TaskId;
    public SubagentRunStatus Status => _run.Status;
    public string AgentName => _run.AgentName;
    public string DisplayMetadata => _run.DisplayMetadata;
    public string? Summary => _run.Summary;
    public string Reference => _run.Reference;
    public string OwnerReference => _run.OwnerInstanceId.Length <= 8
        ? _run.OwnerInstanceId : _run.OwnerInstanceId[..8];
    public string OwnerLabel => InspectorText.Format("SubagentRunOwnerFormat", OwnerReference);
    public string RecoveryHint => InspectorText.Get("SubagentRunRecoveryHint");
    public bool NeedsRecovery { get; }
    public bool CanAcknowledge => NeedsRecovery && AcknowledgeCommand is not null;
    public IRelayCommand<SubagentRunItemViewModel>? AcknowledgeCommand { get; }
    public bool IsConfirmationPending
    {
        get => _isConfirmationPending;
        internal set
        {
            if (SetProperty(ref _isConfirmationPending, value))
                OnPropertyChanged(nameof(AcknowledgeLabel));
        }
    }
    public string AcknowledgeLabel => InspectorText.Get(IsConfirmationPending
        ? "SubagentRunConfirmInterrupted" : "SubagentRunAcknowledge");
    public string StatusLabel => NeedsRecovery
        ? InspectorText.Get("SubagentRunNeedsReview")
        : _run.StatusLabel;
}
