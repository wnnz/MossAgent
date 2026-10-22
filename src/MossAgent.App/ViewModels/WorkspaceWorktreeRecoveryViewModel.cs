using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeRecoveryViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceProjectService _projects;
    private readonly CancellationTokenSource _lifetime = new();
    private Guid? _projectId;
    private int _generation;
    private bool _isBusy;
    private bool _hasError;
    private bool _disposed;

    public WorkspaceWorktreeRecoveryViewModel(WorkspaceProjectService projects)
    {
        _projects = projects;
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync, () => CanRefresh);
    }

    public ObservableCollection<WorkspaceWorktreeRecoveryItemViewModel> Items { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public bool IsVisible => _projectId is not null && (Items.Count > 0 || HasError || IsBusy);
    public bool HasItems => Items.Count > 0;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyStateChanged(); } }
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public bool CanRefresh => _projectId is not null && !IsBusy && !_disposed;
    public string Title => string.Format(SettingsText.Get("WorktreeRecoveryTitle"), Items.Count);
    public string Hint => SettingsText.Get("WorktreeRecoveryHint");
    public string RefreshLabel => SettingsText.Get("WorktreeRecoveryRefresh");
    public string Error => HasError ? SettingsText.Get("WorktreeRecoveryLoadFailed") : string.Empty;

    public void Show(Guid projectId)
    {
        if (_disposed || projectId == Guid.Empty) return;
        _generation++;
        _projectId = projectId;
        Items.Clear();
        HasError = false;
        NotifyStateChanged();
        _ = RefreshCoreAsync(false, CancellationToken.None);
    }

    public void Clear()
    {
        if (_disposed) return;
        _generation++;
        _projectId = null;
        Items.Clear();
        HasError = false;
        NotifyStateChanged();
    }

    private Task RefreshAsync(CancellationToken cancellationToken) => RefreshCoreAsync(true, cancellationToken);

    private async Task RefreshCoreAsync(bool showBusy, CancellationToken cancellationToken)
    {
        var projectId = _projectId;
        var generation = _generation;
        if (projectId is not { } id || _disposed) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        if (showBusy) await AvaloniaUiThread.RunAsync(() => IsBusy = true, CancellationToken.None);
        try
        {
            var intents = await _projects.InspectWorktreeRecoveryAsync(id, linked.Token);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || _projectId != id) return;
                Items.Clear();
                foreach (var intent in intents)
                    Items.Add(new(intent, AcknowledgeAsync));
                HasError = false;
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
        finally
        {
            if (showBusy) await AvaloniaUiThread.RunAsync(() => IsBusy = false, CancellationToken.None);
        }
    }

    private async Task AcknowledgeAsync(WorkspaceWorktreeRecoveryItemViewModel item,
        CancellationToken cancellationToken)
    {
        await _projects.AcknowledgeWorktreeRecoveryAsync(item.Intent, cancellationToken);
        await AvaloniaUiThread.RunAsync(() =>
        {
            Items.Remove(item);
            NotifyStateChanged();
        }, CancellationToken.None);
    }

    private void NotifyStateChanged()
    {
        foreach (var name in new[] { nameof(IsVisible), nameof(HasItems), nameof(CanRefresh),
                     nameof(Title), nameof(Hint), nameof(Error) })
            OnPropertyChanged(name);
        RefreshCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
