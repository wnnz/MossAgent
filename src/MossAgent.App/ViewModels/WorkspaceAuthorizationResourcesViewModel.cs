using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.Authorization;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceAuthorizationResourcesViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceProjectService _projects;
    private readonly CancellationTokenSource _lifetime = new();
    private AuthorizationResourceInspection _inspection = AuthorizationResourceInspection.Empty;
    private Guid? _projectId;
    private int _generation;
    private bool _confirmationPending;
    private bool _isBusy;
    private bool _hasError;
    private bool _disposed;

    public WorkspaceAuthorizationResourcesViewModel(WorkspaceProjectService projects)
    {
        _projects = projects;
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync, () => CanRefresh);
        AbandonCommand = new GuardedAsyncRelayCommand(AbandonAsync, () => HasStaleResources && !IsBusy);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AbandonCommand { get; }
    public bool IsVisible => _projectId is not null && (HasOutstandingResources || HasError || IsBusy);
    public bool HasOutstandingResources => _inspection.OutstandingCount > 0;
    public bool HasStaleResources => _inspection.StaleCount > 0;
    public int OutstandingCount => _inspection.OutstandingCount;
    public int StaleCount => _inspection.StaleCount;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyStateChanged(); } }
    public bool HasError { get => _hasError; private set { if (SetProperty(ref _hasError, value)) NotifyStateChanged(); } }
    public bool CanRefresh => _projectId is not null && !IsBusy && !_disposed;
    public string Status => !HasOutstandingResources ? string.Empty
        : HasStaleResources
            ? string.Format(CultureInfo.CurrentCulture, SettingsText.Get("ProjectAuthorizationResourcesMixed"),
                _inspection.OutstandingCount, _inspection.StaleCount)
            : string.Format(CultureInfo.CurrentCulture, SettingsText.Get("ProjectAuthorizationResourcesActive"),
                _inspection.OutstandingCount);
    public string Hint => HasStaleResources ? SettingsText.Get("ProjectAuthorizationResourcesHint") : string.Empty;
    public string RefreshLabel => SettingsText.Get("ProjectAuthorizationResourcesRefresh");
    public string AbandonLabel => SettingsText.Get(_confirmationPending
        ? "ProjectAuthorizationResourcesConfirmAbandon" : "ProjectAuthorizationResourcesAbandon");
    public string Error => HasError ? SettingsText.Get("ProjectAuthorizationResourcesFailed") : string.Empty;

    public void Show(Guid projectId)
    {
        if (_disposed || projectId == Guid.Empty) return;
        _generation++;
        _projectId = projectId;
        _inspection = AuthorizationResourceInspection.Empty;
        _confirmationPending = false;
        HasError = false;
        NotifyStateChanged();
        _ = RefreshCoreAsync(false);
    }

    public void Clear()
    {
        if (_disposed) return;
        _generation++;
        _projectId = null;
        _inspection = AuthorizationResourceInspection.Empty;
        _confirmationPending = false;
        HasError = false;
        NotifyStateChanged();
    }

    private Task RefreshAsync(CancellationToken cancellationToken) => RefreshCoreAsync(true, cancellationToken);

    private async Task RefreshCoreAsync(bool showBusy, CancellationToken cancellationToken = default)
    {
        var projectId = _projectId;
        var generation = _generation;
        if (projectId is not { } id || _disposed) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        if (showBusy) await AvaloniaUiThread.RunAsync(() => IsBusy = true, CancellationToken.None);
        try
        {
            var inspection = await _projects.InspectAuthorizationResourcesAsync(id, linked.Token);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || _projectId != id) return;
                _inspection = inspection;
                _confirmationPending = false;
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

    private async Task AbandonAsync(CancellationToken cancellationToken)
    {
        if (_projectId is not { } projectId || !HasStaleResources || IsBusy) return;
        if (!_confirmationPending)
        {
            _confirmationPending = true;
            OnPropertyChanged(nameof(AbandonLabel));
            return;
        }
        _confirmationPending = false;
        await AvaloniaUiThread.RunAsync(() => IsBusy = true, CancellationToken.None);
        try
        {
            await _projects.AbandonStaleAuthorizationResourcesAsync(
                projectId, _inspection.StaleLeaseIds, cancellationToken);
            await RefreshCoreAsync(false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                HasError = true;
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        finally { await AvaloniaUiThread.RunAsync(() => IsBusy = false, CancellationToken.None); }
    }

    private void NotifyStateChanged()
    {
        foreach (var name in new[] { nameof(IsVisible), nameof(HasOutstandingResources), nameof(HasStaleResources),
                     nameof(OutstandingCount), nameof(StaleCount),
                     nameof(CanRefresh), nameof(Status), nameof(Hint), nameof(AbandonLabel), nameof(Error) })
            OnPropertyChanged(name);
        RefreshCommand.NotifyCanExecuteChanged();
        AbandonCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
