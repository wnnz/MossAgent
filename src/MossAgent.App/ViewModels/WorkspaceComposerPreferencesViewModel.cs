using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.Settings;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceComposerPreferencesViewModel : ObservableObject, IDisposable
{
    private readonly IComposerPreferenceStore? _store;
    private readonly ICommand _queueSubmit;
    private readonly ICommand _steerSubmit;
    private readonly SettingsOperationLifetime _lifetime = new(TimeSpan.FromSeconds(10));
    private readonly GuardedAsyncRelayCommand _submit;
    private readonly GuardedAsyncRelayCommand _alternateSubmit;
    private readonly GuardedAsyncRelayCommand _save;
    private readonly GuardedAsyncRelayCommand _reload;
    private Task? _load;
    private Task _operations = Task.CompletedTask;
    private ComposerRunningAction _applied = ComposerRunningAction.Queue;
    private ComposerRunningActionOption? _selected;
    private bool _routingReady;
    private bool _loadFailed;
    private bool _busy;
    private bool _disposed;
    private string _error = string.Empty;

    public WorkspaceComposerPreferencesViewModel(IComposerPreferenceStore? store,
        ICommand queueSubmit, ICommand steerSubmit)
    {
        _store = store;
        _queueSubmit = queueSubmit;
        _steerSubmit = steerSubmit;
        Options = [new(ComposerRunningAction.Queue, ComposerPreferenceText.QueueFirst),
            new(ComposerRunningAction.Steer, ComposerPreferenceText.SteerFirst)];
        _selected = Options[0];
        _submit = new GuardedAsyncRelayCommand(_ => Submit(Choose(false)), () => CanSubmit(Choose(false)));
        _alternateSubmit = new GuardedAsyncRelayCommand(_ => Submit(Choose(true)), () => CanSubmit(Choose(true)));
        _save = new GuardedAsyncRelayCommand(SaveAsync, CanSave);
        _reload = new GuardedAsyncRelayCommand(_ => ReloadAsync(), () => !_disposed && _store is not null && !_busy);
        _routingReady = store is null;
        _queueSubmit.CanExecuteChanged += ForwardSourceChanged;
        _steerSubmit.CanExecuteChanged += ForwardSourceChanged;
    }

    public IReadOnlyList<ComposerRunningActionOption> Options { get; }
    public ComposerRunningActionOption? SelectedOption
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value)) { _save.NotifyCanExecuteChanged(); } }
    }
    public ComposerRunningAction Applied => _applied;
    public IAsyncRelayCommand SubmitCommand => _submit;
    public IAsyncRelayCommand AlternateSubmitCommand => _alternateSubmit;
    public IAsyncRelayCommand SaveCommand => _save;
    public IAsyncRelayCommand ReloadCommand => _reload;
    public bool IsBusy { get => _busy; private set => SetProperty(ref _busy, value); }
    public bool IsLoaded => _routingReady;
    public string Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => _error.Length > 0;
    public string Title => ComposerPreferenceText.Title;
    public string Description => ComposerPreferenceText.Description;
    public string DefaultActionLabel => ComposerPreferenceText.DefaultAction;
    public string SaveLabel => ComposerPreferenceText.Save;
    public string ReloadLabel => ComposerPreferenceText.Reload;
    public string Hint => _applied == ComposerRunningAction.Steer
        ? ComposerPreferenceText.SteerFirstHint : ComposerPreferenceText.QueueFirstHint;

    private ICommand Choose(bool alternate) =>
        (alternate != (_applied == ComposerRunningAction.Steer)) ? _steerSubmit : _queueSubmit;

    private bool CanSubmit(ICommand chosen) => !_disposed && _routingReady && chosen.CanExecute(null);

    private Task Submit(ICommand chosen)
    {
        if (!CanSubmit(chosen)) return Task.CompletedTask;
        chosen.Execute(null);
        return Task.CompletedTask;
    }

    private bool CanSave() => !_disposed && !_busy && _store is not null && !_loadFailed && _selected is { Value: var value } && value != _applied;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_selected is null || _store is null) return;
        var operation = _lifetime.CreateOperation(cancellationToken);
        var tracked = SaveCoreAsync(_selected!.Value, operation, cancellationToken);
        _operations = tracked;
        await tracked.ConfigureAwait(false);
    }

    private async Task SaveCoreAsync(ComposerRunningAction action, CancellationTokenSource operation,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        _save.NotifyCanExecuteChanged();
        try
        {
            await _store!.SaveAsync(action, operation.Token).ConfigureAwait(false);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                _applied = action;
                Error = string.Empty;
                NotifyRoutingChanged();
            }, CancellationToken.None);
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await AvaloniaUiThread.RunAsync(() => { if (!_disposed) Error = ComposerPreferenceText.SaveFailed; }, CancellationToken.None);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            await AvaloniaUiThread.RunAsync(() => { if (!_disposed) Error = ComposerPreferenceText.SaveFailed; }, CancellationToken.None);
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                IsBusy = false;
                _save.NotifyCanExecuteChanged();
                _reload.NotifyCanExecuteChanged();
            }, CancellationToken.None);
            operation.Dispose();
        }
    }

    public Task LoadAsync() => _load ??= _store is null ? Task.CompletedTask : LoadCoreAsync();

    private Task ReloadAsync()
    {
        if (_store is null) return Task.CompletedTask;
        _load = LoadCoreAsync();
        return _load;
    }

    private async Task LoadCoreAsync()
    {
        var operation = _lifetime.CreateOperation(CancellationToken.None);
        IsBusy = true;
        _reload.NotifyCanExecuteChanged();
        try
        {
            var loaded = await _store!.LoadAsync(operation.Token).ConfigureAwait(false);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                _applied = loaded;
                _selected = Options.First(option => option.Value == loaded);
                _routingReady = true;
                _loadFailed = false;
                Error = string.Empty;
                OnPropertyChanged(nameof(SelectedOption));
                OnPropertyChanged(nameof(Applied));
                OnPropertyChanged(nameof(IsLoaded));
                NotifyRoutingChanged();
            }, CancellationToken.None);
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested && !_lifetime.Token.IsCancellationRequested)
        {
            await PublishLoadFailureAsync();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            await PublishLoadFailureAsync();
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                IsBusy = false;
                _save.NotifyCanExecuteChanged();
                _reload.NotifyCanExecuteChanged();
            }, CancellationToken.None);
            operation.Dispose();
        }
    }

    private Task PublishLoadFailureAsync() => AvaloniaUiThread.RunAsync(() =>
    {
        if (_disposed) return;
        _routingReady = true;
        _loadFailed = true;
        Error = ComposerPreferenceText.LoadFailed;
        OnPropertyChanged(nameof(IsLoaded));
        NotifyRoutingChanged();
    }, CancellationToken.None);

    private void ForwardSourceChanged(object? sender, EventArgs args) => NotifyRoutingChanged();

    private void NotifyRoutingChanged()
    {
        OnPropertyChanged(nameof(Hint));
        _submit.NotifyCanExecuteChanged();
        _alternateSubmit.NotifyCanExecuteChanged();
    }

    public async Task StopAsync()
    {
        _lifetime.Dispose();
        if (_load is not null)
            await _load.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await _operations.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queueSubmit.CanExecuteChanged -= ForwardSourceChanged;
        _steerSubmit.CanExecuteChanged -= ForwardSourceChanged;
        _submit.Dispose();
        _alternateSubmit.Dispose();
        _save.Dispose();
        _reload.Dispose();
        _lifetime.Dispose();
    }
}
