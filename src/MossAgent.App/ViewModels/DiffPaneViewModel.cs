using System.Text.Json;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class DiffPaneViewModel : ObservableObject, IDisposable
{
    private readonly IToolExecutor _tools;
    private ToolExecutionContext? _context;
    private CancellationTokenSource _scope = new();
    private CancellationTokenSource? _diffCancellation;
    private string _content = string.Empty;
    private string _contentStatus = InspectorText.Get("SelectFile");
    private string _status = InspectorText.Get("SelectTask");
    private IReadOnlyList<GitChangedFileViewModel> _allFiles = [];
    private GitChangedFileViewModel? _selectedFile;
    private bool _isStaged;
    private bool _isLoading;
    private bool _isDiffLoading;
    private bool _hasSnapshot;
    private bool _disposed;
    private int _generation;
    private int _diffVersion;

    public DiffPaneViewModel(IToolExecutor tools)
    {
        _tools = tools;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => HasTask && !IsLoading);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public ObservableCollection<GitChangedFileViewModel> Files { get; } = [];
    public bool HasTask => _context is not null && !_disposed;
    public bool HasFiles => Files.Count > 0;
    public bool HasContent => !string.IsNullOrWhiteSpace(Content);
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) RefreshCommand.NotifyCanExecuteChanged(); } }
    public bool IsDiffLoading { get => _isDiffLoading; private set => SetProperty(ref _isDiffLoading, value); }
    public string Content { get => _content; private set { if (SetProperty(ref _content, value)) OnPropertyChanged(nameof(HasContent)); } }
    public string ContentStatus { get => _contentStatus; private set => SetProperty(ref _contentStatus, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public GitChangedFileViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (value is not null && !Files.Any(file => ReferenceEquals(file, value))) return;
            if (ReferenceEquals(_selectedFile, value)) return;
            _selectedFile = value;
            OnPropertyChanged();
            _ = RefreshSelectedDiffAsync();
        }
    }
    public bool IsStaged
    {
        get => _isStaged;
        set { if (SetProperty(ref _isStaged, value)) ApplyFilter(); }
    }

    public void Attach(ToolExecutionContext? context)
    {
        if (_disposed) return;
        _generation++;
        _scope.Cancel();
        _scope.Dispose();
        _scope = new();
        _context = context;
        _hasSnapshot = false;
        Show(string.Empty);
        ContentStatus = InspectorText.Get("SelectFile");
        _allFiles = [];
        Files.Clear();
        SelectedFile = null;
        IsLoading = false;
        Status = InspectorText.Get(context is null ? "SelectTask" : "RefreshDiff");
        OnPropertyChanged(nameof(HasTask));
        OnPropertyChanged(nameof(HasFiles));
        RefreshCommand.NotifyCanExecuteChanged();
    }

    public void Show(string content)
    {
        _diffVersion++;
        _diffCancellation?.Cancel();
        IsDiffLoading = false;
        Content = content;
        ContentStatus = InspectorText.Get("SelectFile");
    }

    public async Task ReviewAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (!HasTask || _context?.TaskId != taskId) return;
        IsStaged = false;
        await RefreshAsync(cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!HasTask || IsLoading) return;
        var context = _context!;
        var generation = _generation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_scope.Token, cancellationToken);
        var token = linked.Token;
        IsLoading = true;
        Status = InspectorText.Get("Loading");
        try
        {
            var arguments = JsonSerializer.SerializeToElement(new { nullTerminated = true });
            var result = await _tools.ExecuteAsync(new ToolRequest(Guid.NewGuid().ToString("N"), "git.status", arguments), context, token);
            if (_disposed || generation != _generation) return;
            _hasSnapshot = result.IsSuccess;
            _allFiles = result.IsSuccess ? GitStatusParser.Parse(result.Content ?? string.Empty) : [];
            ApplyFilter();
            Status = !result.IsSuccess ? InspectorText.Get("DiffFailed") : _allFiles.Count == 0
                ? InspectorText.Get("NoChanges") : InspectorText.Format("DiffCount", _allFiles.Count, Files.Count);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_disposed && generation == _generation) Status = InspectorText.Get("RefreshDiff");
        }
        catch (Exception)
        {
            if (!_disposed && generation == _generation)
            { _hasSnapshot = false; _allFiles = []; ApplyFilter(); Status = InspectorText.Get("DiffFailed"); }
        }
        finally { if (!_disposed && generation == _generation) IsLoading = false; }
    }

    private void ApplyFilter()
    {
        var selectedPath = SelectedFile?.Path;
        Files.ReplaceWith(_allFiles.Where(file => IsStaged ? file.HasStagedChange : file.HasUnstagedChange));
        OnPropertyChanged(nameof(HasFiles));
        var selected = Files.FirstOrDefault(file => file.Path == selectedPath) ?? Files.FirstOrDefault();
        if (ReferenceEquals(SelectedFile, selected)) _ = RefreshSelectedDiffAsync();
        else SelectedFile = selected;
        if (_hasSnapshot && !IsLoading)
            Status = _allFiles.Count == 0 ? InspectorText.Get("NoChanges") : InspectorText.Format("DiffCount", _allFiles.Count, Files.Count);
    }

    private async Task RefreshSelectedDiffAsync()
    {
        var version = ++_diffVersion;
        _diffCancellation?.Cancel();
        _diffCancellation?.Dispose();
        _diffCancellation = null;
        Content = string.Empty;
        IsDiffLoading = false;
        ContentStatus = InspectorText.Get(HasTask ? IsStaged ? "NoStaged" : "NoUnstaged" : "SelectFile");
        if (!HasTask || SelectedFile is not { } file) return;
        var context = _context!;
        var generation = _generation;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_scope.Token);
        _diffCancellation = cancellation;
        var token = cancellation.Token;
        IsDiffLoading = true;
        ContentStatus = InspectorText.Get("Loading");
        try
        {
            var arguments = JsonSerializer.SerializeToElement(new { staged = IsStaged, path = file.Path, untracked = !IsStaged && file.Status == "??" });
            var result = await _tools.ExecuteAsync(new ToolRequest(Guid.NewGuid().ToString("N"), "git.diff", arguments), context, token);
            if (_disposed || generation != _generation || version != _diffVersion) return;
            Content = result.IsSuccess ? result.Content ?? string.Empty : string.Empty;
            ContentStatus = InspectorText.Get(result.IsSuccess ? "NoDiffContent" : "DiffFileFailed");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        { if (!_disposed && generation == _generation && version == _diffVersion) ContentStatus = InspectorText.Get("DiffFileFailed"); }
        finally
        {
            if (!_disposed && generation == _generation && version == _diffVersion) IsDiffLoading = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Attach(null);
        _disposed = true;
        _scope.Cancel();
        _scope.Dispose();
        _diffCancellation?.Cancel();
        _diffCancellation?.Dispose();
        RefreshCommand.NotifyCanExecuteChanged();
    }
}
