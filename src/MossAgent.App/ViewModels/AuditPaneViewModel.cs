using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class AuditPaneViewModel : ObservableObject, IDisposable
{
    private readonly IToolExecutionAuditReader _reader;
    private readonly ISubagentRunJournal? _runJournal;
    private readonly IBrowserSessionJournal? _browserJournal;
    private Guid? _taskId;
    private ToolExecutionAuditRecord? _selectedExecution;
    private AuditSubagentRunViewModel? _selectedSubagentRun;
    private CancellationTokenSource _scope = new();
    private string _status = InspectorText.Get("SelectTask");
    private string _artifactsStatus = InspectorText.Get("ArtifactsHint");
    private bool _isLoading;
    private bool _disposed;
    private int _generation;
    private int _artifactVersion;

    public AuditPaneViewModel(IToolExecutionAuditReader reader, ISubagentRunJournal? runJournal = null,
        IBrowserSessionJournal? browserJournal = null)
    {
        _reader = reader;
        _runJournal = runJournal;
        _browserJournal = browserJournal;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => HasTask && !IsLoading);
    }

    public ObservableCollection<ToolExecutionAuditRecord> Executions { get; } = [];
    public ObservableCollection<AuditExecutionViewModel> ExecutionItems { get; } = [];
    public ObservableCollection<AuditSubagentRunViewModel> SubagentRuns { get; } = [];
    public ObservableCollection<AuditBrowserSessionViewModel> BrowserSessions { get; } = [];
    public ObservableCollection<ToolArtifactRecord> Artifacts { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public bool HasTask => _taskId is not null && !_disposed;
    public bool HasExecutions => Executions.Count > 0;
    public bool HasRuns => SubagentRuns.Count > 0;
    public bool HasBrowserSessions => BrowserSessions.Count > 0;
    public bool HasArtifacts => Artifacts.Count > 0;
    public string RunsTitle => InspectorText.Format("AuditSubagentRunsTitle", SubagentRuns.Count);
    public string BrowserSessionsTitle => InspectorText.Format("AuditBrowserSessionsTitle", BrowserSessions.Count);
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) RefreshCommand.NotifyCanExecuteChanged(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ArtifactsStatus { get => _artifactsStatus; private set => SetProperty(ref _artifactsStatus, value); }
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
    public AuditSubagentRunViewModel? SelectedSubagentRun
    {
        get => _selectedSubagentRun;
        set
        {
            if (value is not null && !SubagentRuns.Any(run => ReferenceEquals(run, value))) return;
            if (ReferenceEquals(_selectedSubagentRun, value)) return;
            _selectedSubagentRun = value;
            OnPropertyChanged();
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
        SubagentRuns.Clear();
        BrowserSessions.Clear();
        Artifacts.Clear();
        SelectedExecution = null;
        SelectedSubagentRun = null;
        IsLoading = false;
        Status = InspectorText.Get(taskId is null ? "SelectTask" : "Loading");
        ArtifactsStatus = InspectorText.Get("ArtifactsHint");
        NotifyCollections();
        OnPropertyChanged(nameof(HasTask));
        RefreshCommand.NotifyCanExecuteChanged();
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
        try
        {
            var runsTask = ReadRunsAsync(taskId, token);
            var browserTask = ReadBrowserSessionsAsync(taskId, token);
            var records = await _reader.GetRecentAsync(taskId, 200, token);
            var runs = await runsTask;
            var sessions = await browserTask;
            if (_disposed || generation != _generation) return;
            var selectionId = SelectedExecution?.Id;
            var selectedRunId = SelectedSubagentRun?.Record.RunId;
            Executions.ReplaceWith(records.Where(record => record.TaskId == taskId).DistinctBy(record => record.Id).Take(200));
            ExecutionItems.ReplaceWith(Executions.Select(record => new AuditExecutionViewModel(record)));
            SubagentRuns.ReplaceWith(runs.Where(run => run.TaskId == taskId).DistinctBy(run => run.RunId)
                .Select(run => new AuditSubagentRunViewModel(run)));
            BrowserSessions.ReplaceWith(sessions);
            SelectedExecution = Executions.FirstOrDefault(record => record.Id == selectionId) ?? Executions.FirstOrDefault();
            SelectedSubagentRun = SubagentRuns.FirstOrDefault(run => run.Record.RunId == selectedRunId)
                ?? SubagentRuns.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedItem));
            Status = !HasExecutions ? InspectorText.Get("NoAuditHint") : InspectorText.Format("AuditCount", Executions.Count);
            NotifyCollections();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed && generation == _generation) Status = InspectorText.Get("AuditFailed"); }
        finally { if (!_disposed && generation == _generation) IsLoading = false; }
    }

    // The run journal read is best-effort: a missing journal leaves an empty list and a
    // failed read must never break the executions main path.
    private async Task<IReadOnlyList<SubagentRunRecord>> ReadRunsAsync(Guid taskId, CancellationToken token)
    {
        if (_runJournal is null) return [];
        try { return await _runJournal.ReadTaskAsync(taskId, token); }
        catch (Exception) { return []; }
    }

    // The browser journal reads are best-effort for the same reason: a missing or
    // failing journal degrades to no browser rows and never breaks the tool audit path.
    private async Task<IReadOnlyList<AuditBrowserSessionViewModel>> ReadBrowserSessionsAsync(
        Guid taskId, CancellationToken token)
    {
        if (_browserJournal is null) return [];
        try
        {
            var sessions = await _browserJournal.ReadSessionsAsync(taskId, token);
            var intents = await _browserJournal.ReadIntentsAsync(taskId, token);
            return sessions.Where(session => session.TaskId == taskId).DistinctBy(session => session.SessionId)
                .Select(session => new AuditBrowserSessionViewModel(session,
                    intents.Where(intent => intent.SessionId == session.SessionId)
                        .OrderBy(intent => intent.CreatedAt).ToArray()))
                .ToArray();
        }
        catch (Exception) { return []; }
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
        OnPropertyChanged(nameof(HasRuns));
        OnPropertyChanged(nameof(RunsTitle));
        OnPropertyChanged(nameof(HasBrowserSessions));
        OnPropertyChanged(nameof(BrowserSessionsTitle));
        OnPropertyChanged(nameof(HasArtifacts));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Attach(null);
        _disposed = true;
        _scope.Cancel();
        _scope.Dispose();
        RefreshCommand.NotifyCanExecuteChanged();
    }
}
