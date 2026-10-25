using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceArtifactRecoveryItemViewModel : ObservableObject
{
    internal WorkspaceArtifactRecoveryItemViewModel(ArtifactSessionRecord record,
        Func<WorkspaceArtifactRecoveryItemViewModel, Task> acknowledge, Func<bool> canMutate)
    {
        Record = record;
        AcknowledgeCommand = new AsyncRelayCommand(() => acknowledge(this), canMutate);
    }

    internal ArtifactSessionRecord Record { get; }
    public long RecordId => Record.Id;
    public Guid TaskId => Record.TaskId;
    public string FileName => Record.FileName;
    public string StageLabel => WorkspaceText.Get(Record.ErrorCode switch
    {
        "staged" => "ArtifactRecoveryStageStaged",
        "completed" => "ArtifactRecoveryStageCompleted",
        "cleanup_unknown" => "ArtifactRecoveryStageCleanupUnknown",
        "unverified" => "ArtifactRecoveryStageUnverified",
        "sdk_temp" => "ArtifactRecoveryStageSdkTemp",
        _ => "ArtifactRecoveryStageUnknown",
    });
    public string KindLabel => WorkspaceText.Get(Record.Kind switch
    {
        ArtifactSessionKind.DiffInput => "ArtifactRecoveryKindDiffInput",
        ArtifactSessionKind.ExternalSdkTemp => "ArtifactRecoveryKindSdkTemp",
        _ => "ArtifactRecoveryKindArtifact",
    });
    public string Summary => $"{StageLabel} · {KindLabel}";
    public string Detail => string.Format(WorkspaceText.Get("ArtifactRecoveryDetail"),
        TaskId.ToString("N")[..8], Record.CreatedAt.ToLocalTime().ToString("g"));
    public string AcknowledgeLabel => WorkspaceText.Get("WorktreeRecoveryAcknowledge");
    public IAsyncRelayCommand AcknowledgeCommand { get; }

    internal void NotifyCommands() => AcknowledgeCommand.NotifyCanExecuteChanged();
}
