using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using MossAgent.Application.Artifacts;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class FilePreviewPaneViewModel : ObservableObject, IDisposable
{
    public const int MaximumTabs = 8;
    private readonly IToolExecutor _tools;
    private readonly ITaskArtifactContentReader _reader;
    private ToolExecutionContext? _context;
    private FilePreviewTabViewModel? _active;
    private bool _isOpen;
    private bool _disposed;
    private int _generation;
    private string _notice = string.Empty;
    private CancellationTokenSource? _revealCancellation;
    private int _revealGeneration;

    public FilePreviewPaneViewModel(IToolExecutor tools, ITaskArtifactContentReader reader, IFilePickerService? filePicker = null)
    {
        _tools = tools;
        _reader = reader;
        OpenCommand = new AsyncRelayCommand<string>((path, token) => OpenAsync(path, token), path => HasTask && !string.IsNullOrWhiteSpace(path),
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        OpenArtifactCommand = new AsyncRelayCommand<ToolArtifactRecord>((artifact, token) => OpenArtifactAsync(artifact, token),
            artifact => HasTask && artifact?.TaskId == _context?.TaskId);
        SelectCommand = new RelayCommand<FilePreviewTabViewModel>(Select, tab => tab is not null && Tabs.Contains(tab));
        CloseCommand = new RelayCommand<FilePreviewTabViewModel>(Close, tab => tab is not null && Tabs.Contains(tab));
        HideCommand = new RelayCommand(Hide);
        RefreshCommand = new AsyncRelayCommand(token => LoadAsync(Active!, token), () => HasTask && Active is { IsLoading: false });
        RevealCommand = new AsyncRelayCommand(RevealAsync, () => CanReveal);
        FilePicker = new FilePreviewFilePickerViewModel(this, filePicker);
    }

    public event Action? Opened;
    public FilePreviewFilePickerViewModel FilePicker { get; }
    internal int ContextVersion => _generation;
    public ObservableCollection<FilePreviewTabViewModel> Tabs { get; } = [];
    public FilePreviewTabViewModel? Active
    {
        get => _active;
        private set { if (!ReferenceEquals(_active, value)) CancelReveal(); SetProperty(ref _active, value); NotifyState(); }
    }
    public bool HasTask => _context is not null && !_disposed;
    public bool IsOpen { get => _isOpen; private set { if (SetProperty(ref _isOpen, value)) NotifyState(); } }
    public string Notice { get => _notice; private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => !string.IsNullOrEmpty(Notice);
    public IAsyncRelayCommand<string> OpenCommand { get; }
    public IAsyncRelayCommand<ToolArtifactRecord> OpenArtifactCommand { get; }
    public IRelayCommand<FilePreviewTabViewModel> SelectCommand { get; }
    public IRelayCommand<FilePreviewTabViewModel> CloseCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand HideCommand { get; }
    public IAsyncRelayCommand RevealCommand { get; }
    public bool CanReveal => HasTask && IsOpen && _revealCancellation is null && Active is { IsReady: true, IsLoading: false, IsStoredArtifact: false, Change: null }
        && _context!.ApprovalPolicy != MossAgent.Domain.ApprovalPolicy.ReadOnly;

    public void Attach(ToolExecutionContext? context)
    {
        if (_disposed) return;
        if (_context is not null && context is not null && _context.TaskId == context.TaskId
            && _context.WorkingDirectory.Equals(context.WorkingDirectory, StringComparison.OrdinalIgnoreCase)
            && _context.ArtifactDirectory == context.ArtifactDirectory && _context.ApprovalPolicy == context.ApprovalPolicy
            && _context.AuthorizedRoots.SequenceEqual(context.AuthorizedRoots, StringComparer.OrdinalIgnoreCase))
        { _context = context; return; }
        _generation++;
        CancelReveal();
        foreach (var tab in Tabs) tab.Dispose();
        Tabs.Clear();
        _context = context;
        Active = null;
        IsOpen = false;
        Notice = string.Empty;
        NotifyState();
    }

    public void Hide() { CancelReveal(); IsOpen = false; }

    private void CancelReveal() { _revealGeneration++; _revealCancellation?.Cancel(); }

    public async Task OpenAsync(string? path, CancellationToken token = default)
    {
        if (!HasTask || string.IsNullOrWhiteSpace(path)) return;
        string fullPath;
        try { fullPath = System.IO.Path.GetFullPath(path, _context!.WorkingDirectory); }
        catch (Exception) { Notice = InspectorText.Get("PreviewFailed"); return; }
        var tab = GetOrAdd(fullPath, System.IO.Path.GetFileName(fullPath));
        if (tab is null) return;
        Select(tab);
        if (tab.Artifact is null && !tab.IsLoading) await LoadAsync(tab, token);
    }

    public async Task OpenArtifactAsync(ToolArtifactRecord? artifact, CancellationToken token = default)
    {
        if (!HasTask || artifact is null || artifact.TaskId != _context!.TaskId) return;
        var tab = GetOrAdd(artifact.Path, artifact.Name);
        if (tab is null) return;
        tab.IsStoredArtifact = true;
        tab.Artifact = new ToolArtifact(artifact.Name, artifact.Path, artifact.MediaType, artifact.Length, artifact.Sha256);
        Select(tab);
        await LoadAsync(tab, token);
    }

    public async Task OpenChangeAsync(ToolFileChange change, CancellationToken token = default)
    {
        if (!HasTask || !change.IsValid) return;
        var tab = GetOrAdd(change.Path, System.IO.Path.GetFileName(change.Path), change);
        if (tab is null) return;
        Select(tab);
        if (tab.Artifact is null && !tab.IsLoading) await LoadAsync(tab, token);
    }

    private FilePreviewTabViewModel? GetOrAdd(string path, string name, ToolFileChange? change = null)
    {
        Notice = string.Empty;
        var existing = Tabs.FirstOrDefault(tab => tab.Path.Equals(path, StringComparison.OrdinalIgnoreCase) && tab.Change == change);
        if (existing is not null) return existing;
        if (Tabs.Count >= MaximumTabs) { Notice = InspectorText.Get("PreviewTabLimit"); return null; }
        var relative = System.IO.Path.GetRelativePath(_context!.WorkingDirectory, path);
        var outsideWorkingDirectory = System.IO.Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
        var displayPath = outsideWorkingDirectory ? name :
            System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(_context.WorkingDirectory)) + " > " + relative.Replace(System.IO.Path.DirectorySeparatorChar.ToString(), " > ", StringComparison.Ordinal);
        var created = new FilePreviewTabViewModel(path, name, displayPath, outsideWorkingDirectory ? null : relative) { Change = change };
        Tabs.Add(created);
        NotifyState();
        return created;
    }

    private void Select(FilePreviewTabViewModel? tab)
    {
        if (tab is null || !Tabs.Contains(tab) || !HasTask) return;
        foreach (var item in Tabs) item.IsSelected = ReferenceEquals(item, tab);
        Active = tab;
        IsOpen = true;
        Opened?.Invoke();
    }

    private void Close(FilePreviewTabViewModel? tab)
    {
        if (tab is null || !Tabs.Contains(tab)) return;
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        tab.Dispose();
        if (ReferenceEquals(Active, tab))
        {
            Active = null;
            if (Tabs.Count > 0) Select(Tabs[Math.Min(index, Tabs.Count - 1)]);
            else IsOpen = false;
        }
        NotifyState();
    }

    private async Task LoadAsync(FilePreviewTabViewModel tab, CancellationToken commandToken)
    {
        if (!HasTask || !Tabs.Contains(tab)) return;
        if (ReferenceEquals(tab, Active)) CancelReveal();
        var context = _context!;
        var generation = _generation;
        tab.Loading?.Cancel();
        tab.Loading?.Dispose();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        tab.Loading = cancellation;
        tab.IsLoading = true;
        tab.IsReady = false;
        tab.Text = string.Empty;
        tab.Image = null;
        tab.Status = InspectorText.Get("Loading");
        NotifyState();
        bool IsCurrent() => !_disposed && generation == _generation && Tabs.Contains(tab)
            && ReferenceEquals(tab.Loading, cancellation) && !cancellation.IsCancellationRequested;
        try
        {
            var artifact = tab.Artifact;
            if (!tab.IsStoredArtifact)
            {
                var request = tab.Change is { } change
                    ? new ToolRequest(Guid.NewGuid().ToString("N"), "filesystem.review_change", JsonSerializer.SerializeToElement(new { change }))
                    : new ToolRequest(Guid.NewGuid().ToString("N"), "filesystem.preview_file", JsonSerializer.SerializeToElement(new { path = tab.Path }));
                var result = await _tools.ExecuteAsync(request, context with { Timeout = TimeSpan.FromSeconds(30), ReviewedChange = tab.Change }, cancellation.Token);
                if (!IsCurrent()) return;
                if (!result.IsSuccess) { tab.Status = tab.Change is null ? InspectorText.Get("PreviewFailed") : result.Summary; return; }
                artifact = result.Artifacts?.FirstOrDefault();
            }
            if (artifact is null) throw new InvalidDataException();
            var bytes = await _reader.ReadAsync(context.TaskId, artifact, cancellation.Token);
            if (!IsCurrent()) return;
            tab.Artifact = artifact;
            if (artifact.MediaType.StartsWith("image/", StringComparison.Ordinal))
            {
                using var stream = new MemoryStream(bytes, writable: false);
                tab.Image = new Bitmap(stream);
            }
            else tab.Text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            tab.IsReady = true;
            tab.Status = tab.IsDiff ? tab.Text.Length == 0 ? InspectorText.Get("SnapshotNoTextDiff")
                : InspectorText.Format("SnapshotPreviewSize", bytes.Length / 1024d) : InspectorText.Format("PreviewSize", bytes.Length / 1024d);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (!_disposed && generation == _generation && Tabs.Contains(tab)) tab.Status = InspectorText.Get("PreviewCancelled"); }
        catch (Exception)
        { if (IsCurrent()) tab.Status = InspectorText.Get("PreviewFailed"); }
        finally
        {
            if (ReferenceEquals(tab.Loading, cancellation))
            {
                if (cancellation.IsCancellationRequested) tab.Status = InspectorText.Get("PreviewCancelled");
                tab.Loading = null;
                tab.IsLoading = false;
                if (generation == _generation) NotifyState();
            }
        }
    }

    private async Task RevealAsync(CancellationToken commandToken)
    {
        if (!CanReveal || Active is not { } tab) return;
        var context = _context!;
        var generation = _generation;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        _revealCancellation = cancellation;
        var revealGeneration = _revealGeneration;
        bool IsCurrent() => !_disposed && IsOpen && generation == _generation && revealGeneration == _revealGeneration
            && ReferenceEquals(tab, Active) && Tabs.Contains(tab);
        NotifyState();
        try
        {
            var request = new ToolRequest(Guid.NewGuid().ToString("N"), "filesystem.reveal_file",
                JsonSerializer.SerializeToElement(new { path = tab.Path }));
            var result = await _tools.ExecuteAsync(request, context with { Timeout = TimeSpan.FromSeconds(30) }, cancellation.Token);
            if (IsCurrent())
                Notice = InspectorText.Get(cancellation.IsCancellationRequested ? "PreviewRevealCancelled"
                    : result.IsSuccess ? "PreviewRevealed" : "PreviewRevealFailed");
        }
        catch (OperationCanceledException)
        { if (IsCurrent()) Notice = InspectorText.Get("PreviewRevealCancelled"); }
        catch (Exception)
        { if (IsCurrent()) Notice = InspectorText.Get("PreviewRevealFailed"); }
        finally
        {
            if (ReferenceEquals(_revealCancellation, cancellation)) _revealCancellation = null;
            NotifyState();
        }
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasTask));
        OnPropertyChanged(nameof(ContextVersion));
        OpenCommand.NotifyCanExecuteChanged();
        OpenArtifactCommand.NotifyCanExecuteChanged();
        SelectCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanReveal));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Attach(null);
        FilePicker.Dispose();
        _disposed = true;
        Opened = null;
        NotifyState();
    }
}
