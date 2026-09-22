using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class AuditPaneViewModel : ObservableObject
{
    private readonly IToolExecutionAuditReader _reader;
    private Guid? _taskId;
    private ToolExecutionAuditRecord? _selectedExecution;
    private string _status = "请选择任务。";

    public AuditPaneViewModel(IToolExecutionAuditReader reader)
    {
        _reader = reader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => _taskId is not null);
    }

    public ObservableCollection<ToolExecutionAuditRecord> Executions { get; } = [];
    public ObservableCollection<ToolArtifactRecord> Artifacts { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public ToolExecutionAuditRecord? SelectedExecution
    {
        get => _selectedExecution;
        set
        {
            if (SetProperty(ref _selectedExecution, value))
            {
                _ = LoadArtifactsAsync(value);
            }
        }
    }

    public void Attach(Guid taskId)
    {
        _taskId = taskId;
        Executions.Clear();
        Artifacts.Clear();
        SelectedExecution = null;
        Status = "点击刷新读取工具审计。";
        RefreshCommand.NotifyCanExecuteChanged();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_taskId is not { } taskId)
        {
            return;
        }

        var records = await _reader.GetRecentAsync(taskId, 200, CancellationToken.None);
        Executions.ReplaceWith(records);
        SelectedExecution = Executions.FirstOrDefault();
        Status = records.Count == 0 ? "当前任务还没有工具调用。" : $"共 {records.Count} 条工具审计。";
    }

    private async Task LoadArtifactsAsync(ToolExecutionAuditRecord? execution)
    {
        Artifacts.Clear();
        if (execution is null)
        {
            return;
        }

        var records = await _reader.GetArtifactsAsync(execution.Id, CancellationToken.None);
        Artifacts.ReplaceWith(records);
    }
}
