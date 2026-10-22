using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class AuditPaneViewModel : ObservableObject, IDisposable
{
    private readonly IToolExecutionAuditReader _reader;
    private readonly ISubagentRunRepository? _subagentRuns;
    private Guid? _taskId;
    private ToolExecutionAuditRecord? _selectedExecution;
    private CancellationTokenSource _scope = new();
    private string _status = InspectorText.Get("SelectTask");
    private string _artifactsStatus = InspectorText.Get("ArtifactsHint");
    private bool _isLoading;
    private bool _disposed;
    private int _generation;
    private int _artifactVersion;
    private string _subagentRunsStatus = string.Empty;

    public AuditPaneViewModel(IToolExecutionAuditReader reader, ISubagentRunRepository? subagentRuns = null)
    {
        _reader = reader;
        _subagentRuns = subagentRuns;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => HasTask && !IsLoading);
        AcknowledgeSubagentRunCommand = new AsyncRelayCommand<SubagentRunItemViewModel>(
            AcknowledgeSubagentRunAsync, item => item?.CanAcknowledge == true && !IsLoading);
    }

    public ObservableCollection<ToolExecutionAuditRecord> Executions { get; } = [];
    public ObservableCollection<AuditExecutionViewModel> ExecutionItems { get; } = [];
    public ObservableCollection<ToolArtifactRecord> Artifacts { get; } = [];
    public ObservableCollection<SubagentRunItemViewModel> SubagentRuns { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<SubagentRunItemViewModel> AcknowledgeSubagentRunCommand { get; }
    public bool HasTask => _taskId is not null && !_disposed;
    public bool HasExecutions => Executions.Count > 0;
    public bool HasArtifacts => Artifacts.Count > 0;
    public bool HasSubagentRuns => SubagentRuns.Count > 0;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value)) return;
            RefreshCommand.NotifyCanExecuteChanged();
            AcknowledgeSubagentRunCommand.NotifyCanExecuteChanged();
        }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ArtifactsStatus { get => _artifactsStatus; private set => SetProperty(ref _artifactsStatus, value); }
    public string SubagentRunsStatus
    {
        get => _subagentRunsStatus;
        private set
        {
            if (SetProperty(ref _subagentRunsStatus, value))
                OnPropertyChanged(nameof(HasSubagentRunsStatus));
        }
    }
    public bool HasSubagentRunsStatus => !string.IsNullOrWhiteSpace(SubagentRunsStatus);
    public ToolExecutionAuditRecord? SelectedExecution
    {
        get => _selectedExecution;
        set
        {
            if (value is not null && !Executions.Any(record => ReferenceEquals(record, value))) return;
            if (ReferenceEquals(_selectedExecution, value)) return;
            _selectedExecution = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedItem));
            _ = LoadArtifactsAsync(value, ++_artifactVersion);
        }
    }
    public AuditExecutionViewModel? SelectedItem
    {
        get => ExecutionItems.FirstOrDefault(item => ReferenceEquals(item.Record, SelectedExecution));
        set
        {
            if (value is null || ExecutionItems.Contains(value)) SelectedExecution = value?.Record;
        }
    }

    public void Attach(Guid? taskId)
    {
        if (_disposed) return;
        _generation++;
        _artifactVersion++;
        _scope.Cancel();
        _scope.Dispose();
        _scope = new();
        _taskId = taskId;
        Executions.Clear();
        ExecutionItems.Clear();
        Artifacts.Clear();
        SubagentRuns.Clear();
        SelectedExecution = null;
        IsLoading = false;
        Status = InspectorText.Get(taskId is null ? "SelectTask" : "Loading");
        ArtifactsStatus = InspectorText.Get("ArtifactsHint");
        SubagentRunsStatus = string.Empty;
        NotifyCollections();
        OnPropertyChanged(nameof(HasTask));
        RefreshCommand.NotifyCanExecuteChanged();
        AcknowledgeSubagentRunCommand.NotifyCanExecuteChanged();
        if (taskId is not null) _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!HasTask || IsLoading) return;
        var taskId = _taskId!.Value;
        var generation = _generation;
        var token = _scope.Token;
        IsLoading = true;
        Status = InspectorText.Get("Loading");
        SubagentRunsStatus = string.Empty;
        try
        {
            var subagentRuns = await ReadSubagentRunsAsync(taskId, token);
            if (_disposed || generation != _generation) return;
            var owner = _subagentRuns?.InstanceId ?? string.Empty;
            SubagentRuns.ReplaceWith(subagentRuns.Where(run => run.TaskId == taskId).Take(50)
                .Select(run => new SubagentRunItemViewModel(run, owner, AcknowledgeSubagentRunCommand)));
            if (SubagentRunsStatus != RunHistoryUnavailable)
                SubagentRunsStatus = _subagentRuns is null ? string.Empty
                    : SubagentRuns.Count == 0 ? string.Empty
                    : InspectorText.Format("SubagentRunsCount", SubagentRuns.Count);
            OnPropertyChanged(nameof(HasSubagentRuns));
            var records = await _reader.GetRecentAsync(taskId, 200, token);
            if (_disposed || generation != _generation) return;
            var selectionId = SelectedExecution?.Id;
            Executions.ReplaceWith(records.Where(record => record.TaskId == taskId).DistinctBy(record => record.Id).Take(200));
            ExecutionItems.ReplaceWith(Executions.Select(record => new AuditExecutionViewModel(record)));
            SelectedExecution = Executions.FirstOrDefault(record => record.Id == selectionId) ?? Executions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedItem));
            Status = !HasExecutions ? InspectorText.Get("NoAuditHint") : InspectorText.Format("AuditCount", Executions.Count);
            NotifyCollections();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_disposed && generation == _generation)
            {
                Status = InspectorText.Get("AuditFailed");
            }
        }
        finally { if (!_disposed && generation == _generation) IsLoading = false; }
    }

    private async Task AcknowledgeSubagentRunAsync(SubagentRunItemViewModel? item,
        CancellationToken cancellationToken)
    {
        if (_subagentRuns is null || item is null || _taskId is null || !item.NeedsRecovery) return;
        if (!item.IsConfirmationPending)
        {
            item.IsConfirmationPending = true;
            return;
        }
        var taskId = _taskId.Value;
        var generation = _generation;
        try
        {
            var updated = await _subagentRuns.MarkInterruptedAsync(taskId, item.Id,
                "用户已确认旧实例已停止，并已人工核实子代理副作用。", cancellationToken);
            if (_disposed || generation != _generation || _taskId != taskId) return;
            var index = SubagentRuns.IndexOf(item);
            if (index >= 0)
                SubagentRuns[index] = new SubagentRunItemViewModel(updated, _subagentRuns.InstanceId,
                    AcknowledgeSubagentRunCommand);
            SubagentRunsStatus = InspectorText.Format("SubagentRunsCount", SubagentRuns.Count);
            OnPropertyChanged(nameof(HasSubagentRuns));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            if (!_disposed && generation == _generation)
                SubagentRunsStatus = InspectorText.Get("SubagentRunAcknowledgeFailed");
        }
    }

    private async Task<IReadOnlyList<SubagentRun>> ReadSubagentRunsAsync(Guid taskId,
        CancellationToken cancellationToken)
    {
        if (_subagentRuns is null) return [];
        try
        {
            return await _subagentRuns.GetRecentAsync(taskId, 50, cancellationToken)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            SubagentRunsStatus = RunHistoryUnavailable;
            return [];
        }
    }

    private async Task LoadArtifactsAsync(ToolExecutionAuditRecord? execution, int version)
    {
        Artifacts.Clear();
        OnPropertyChanged(nameof(HasArtifacts));
        ArtifactsStatus = InspectorText.Get("ArtifactsHint");
        if (_disposed || execution is null || execution.TaskId != _taskId) return;
        var generation = _generation;
        var token = _scope.Token;
        ArtifactsStatus = InspectorText.Get("Loading");
        try
        {
            var records = await _reader.GetArtifactsAsync(execution.Id, token);
            if (_disposed || generation != _generation || version != _artifactVersion) return;
            var owned = records.Where(record => record.TaskId == execution.TaskId && record.ExecutionId == execution.Id)
                .DistinctBy(record => record.Id).Take(51).ToArray();
            Artifacts.ReplaceWith(owned.Take(50));
            ArtifactsStatus = owned.Length > 50 ? InspectorText.Get("ArtifactsLimited")
                : owned.Length == 0 ? InspectorText.Get("NoArtifacts") : InspectorText.Format("ArtifactsCount", owned.Length);
            OnPropertyChanged(nameof(HasArtifacts));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        { if (!_disposed && generation == _generation && version == _artifactVersion) ArtifactsStatus = InspectorText.Get("ArtifactsFailed"); }
    }

    private void NotifyCollections()
    {
        OnPropertyChanged(nameof(HasExecutions));
        OnPropertyChanged(nameof(HasArtifacts));
        OnPropertyChanged(nameof(HasSubagentRuns));
    }

    private static string RunHistoryUnavailable => InspectorText.Get("SubagentRunsFailed");

    public void Dispose()
    {
        if (_disposed) return;
        Attach(null);
        _disposed = true;
        _scope.Cancel();
        _scope.Dispose();
        RefreshCommand.NotifyCanExecuteChanged();
        AcknowledgeSubagentRunCommand.NotifyCanExecuteChanged();
    }
}
