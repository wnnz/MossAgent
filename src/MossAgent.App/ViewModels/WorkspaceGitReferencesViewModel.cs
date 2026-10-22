using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceGitReferencesViewModel : ObservableObject, IDisposable
{
    private const int PageSize = 20;
    private readonly IGitReferenceDiscoveryService? _service;
    private readonly Action<string> _selectCommit;
    private readonly CancellationTokenSource _lifetime = new();
    private ProjectProfile? _project;
    private string _search = string.Empty;
    private string _preview = string.Empty;
    private bool _enabled;
    private bool _isBusy;
    private bool _hasError;
    private bool _hasMore;
    private int _page;
    private int _generation;
    private bool _disposed;

    public WorkspaceGitReferencesViewModel(IGitReferenceDiscoveryService? service, Action<string> selectCommit)
    {
        _service = service;
        _selectCommit = selectCommit;
        SearchCommand = new GuardedAsyncRelayCommand(token => LoadAsync(true, token), () => CanLoad);
        MoreCommand = new GuardedAsyncRelayCommand(token => LoadAsync(false, token), () => CanLoad && HasMore);
    }

    public ObservableCollection<WorkspaceGitReferenceItemViewModel> Branches { get; } = [];
    public ObservableCollection<WorkspaceGitReferenceItemViewModel> Tags { get; } = [];
    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand MoreCommand { get; }
    public bool IsAvailable => _service is not null;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyStateChanged(); } }
    public bool HasError { get => _hasError; private set { if (SetProperty(ref _hasError, value)) NotifyStateChanged(); } }
    public bool HasMore { get => _hasMore; private set { if (SetProperty(ref _hasMore, value)) NotifyStateChanged(); } }
    public bool CanLoad => IsAvailable && _enabled && _project is not null && !IsBusy && !_disposed;
    public bool HasBranches => Branches.Count > 0;
    public bool HasTags => Tags.Count > 0;
    public bool HasResults => HasBranches || HasTags;
    public bool IsEmpty => !IsBusy && !HasError && !HasResults;
    public bool HasPreview => !string.IsNullOrWhiteSpace(_preview);
    public string SearchLabel => WorkspaceText.Get("GitReferenceSearch");
    public string SearchPlaceholder => WorkspaceText.Get("GitReferenceSearchPlaceholder");
    public string BranchLabel => WorkspaceText.Get("GitReferenceBranches");
    public string TagLabel => WorkspaceText.Get("GitReferenceTags");
    public string MoreLabel => WorkspaceText.Get("GitReferenceMore");
    public string EmptyLabel => WorkspaceText.Get("GitReferenceEmpty");
    public string ErrorLabel => HasError ? WorkspaceText.Get("GitReferenceLoadFailed") : string.Empty;
    public string Preview => _preview;
    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty)) HasMore = false;
        }
    }

    public void SetProject(ProjectProfile? project, bool enabled)
    {
        _generation++;
        _project = project;
        _enabled = enabled;
        Search = string.Empty;
        ClearItems();
        ClearPreview();
        NotifyStateChanged();
    }

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        if (!enabled) ClearItems();
        NotifyStateChanged();
    }

    public void Open()
    {
        if (_enabled && _project is not null && !HasResults && !IsBusy) _ = LoadAsync(true, CancellationToken.None);
    }

    public void ClearPreview()
    {
        _preview = string.Empty;
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(HasPreview));
    }

    private async Task LoadAsync(bool reset, CancellationToken cancellationToken)
    {
        if (_project is not { } project || !CanLoad) return;
        var generation = _generation;
        var page = reset ? 1 : _page + 1;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IsBusy = true;
        HasError = false;
        try
        {
            var result = await _service!.ListLocalAsync(
                project, Search.Trim(), page, PageSize, linked.Token);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || _project?.Id != project.Id) return;
                if (reset) ClearItems();
                foreach (var item in result.Items)
                    (item.Kind == GitReferenceKind.Branch ? Branches : Tags).Add(
                        new(item, SelectAsync));
                _page = result.Page;
                HasMore = result.HasMore;
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation) return;
                HasError = true;
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        finally { await AvaloniaUiThread.RunAsync(() => IsBusy = false, CancellationToken.None); }
    }

    private async Task SelectAsync(WorkspaceGitReferenceItemViewModel item)
    {
        if (_project is not { } project || !_enabled) return;
        try
        {
            var preview = await _service!.PreviewAsync(
                project, item.Suggestion.FullName, _lifetime.Token);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || _project?.Id != project.Id) return;
                _selectCommit(preview.Commit);
                _preview = string.Format(WorkspaceText.Get("GitReferencePreview"), item.KindLabel,
                    item.Name, preview.Commit[..Math.Min(12, preview.Commit.Length)], preview.Subject);
                OnPropertyChanged(nameof(Preview));
                OnPropertyChanged(nameof(HasPreview));
            }, CancellationToken.None);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (!_disposed) HasError = true;
            }, CancellationToken.None);
        }
    }

    private void ClearItems()
    {
        Branches.Clear();
        Tags.Clear();
        _page = 0;
        HasMore = false;
    }

    private void NotifyStateChanged()
    {
        foreach (var name in new[] { nameof(IsAvailable), nameof(CanLoad), nameof(HasBranches),
                     nameof(HasTags), nameof(HasResults), nameof(IsEmpty), nameof(ErrorLabel) })
            OnPropertyChanged(name);
        SearchCommand.NotifyCanExecuteChanged();
        MoreCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
