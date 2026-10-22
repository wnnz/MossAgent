using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.Settings;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Persists the composer keyboard mode and exposes the behavior currently used by the workspace.
/// </summary>
public sealed class ComposerPreferenceViewModel : ObservableObject, IDisposable
{
    private readonly IComposerPreferenceStore? _store;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Lazy<Task> _load;
    private ComposerSendMode _persistedMode = ComposerSendMode.EnterSends;
    private int _selectedIndex;
    private int _writeRevision;
    private bool _isReady;
    private bool _applying;
    private bool _disposed;
    private string? _error;

    public ComposerPreferenceViewModel(IComposerPreferenceStore? store = null)
    {
        _store = store;
        _load = new(LoadCoreAsync);
        Options =
        [
            ComposerInputText.EnterSendsOption,
            ComposerInputText.ControlEnterSendsOption
        ];
    }

    public IReadOnlyList<string> Options { get; }
    public bool IsReady { get => _isReady; private set => SetProperty(ref _isReady, value); }
    public ComposerSendMode Mode => SelectedIndex == 1 ? ComposerSendMode.ControlEnterSends : ComposerSendMode.EnterSends;
    public bool SubmitOnEnter => Mode == ComposerSendMode.EnterSends;
    public string Hint => ComposerInputText.HintFor(SubmitOnEnter);
    public string ShortcutSummary => ComposerInputText.ShortcutSummary(SubmitOnEnter);
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (!ApplySelectedIndex(value) || _applying || _disposed) return;
            _ = SaveSelectedAsync(value);
        }
    }

    public Task LoadAsync() => _disposed ? Task.CompletedTask : _load.Value;

    internal async Task SelectIndexAsync(int index, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ApplySelectedIndex(index)) await SaveSelectedAsync(index, cancellationToken);
    }

    private async Task LoadCoreAsync()
    {
        ComposerSendMode mode;
        try
        {
            mode = _store is null ? ComposerSendMode.EnterSends
                : await _store.LoadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or UnauthorizedAccessException or JsonException)
        {
            mode = ComposerSendMode.EnterSends;
            await PublishAsync(() =>
            {
                _persistedMode = mode;
                ApplySelection(mode, notify: false);
                Error = ComposerInputText.PreferenceLoadFailed;
            });
        }
        await PublishAsync(() =>
        {
            _persistedMode = mode;
            ApplySelection(mode, notify: false);
            IsReady = true;
        });
    }

    private async Task SaveSelectedAsync(int index, CancellationToken cancellationToken = default)
    {
        var mode = index == 1 ? ComposerSendMode.ControlEnterSends : ComposerSendMode.EnterSends;
        var revision = Interlocked.Increment(ref _writeRevision);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (revision != Volatile.Read(ref _writeRevision)) return;
            if (_store is not null) await _store.SaveAsync(mode, cancellationToken).ConfigureAwait(false);
            await PublishAsync(() =>
            {
                _persistedMode = mode;
                if (revision == Volatile.Read(ref _writeRevision)) Error = null;
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException && revision == Volatile.Read(ref _writeRevision))
        {
            await PublishAsync(() =>
            {
                if (revision != Volatile.Read(ref _writeRevision)) return;
                ApplySelection(_persistedMode, notify: false);
                Error = ComposerInputText.PreferenceSaveFailed;
            });
        }
        finally { _writeGate.Release(); }
    }

    private bool ApplySelectedIndex(int value)
    {
        if (value is < 0 or > 1 || !SetProperty(ref _selectedIndex, value, nameof(SelectedIndex))) return false;
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(SubmitOnEnter));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(ShortcutSummary));
        return true;
    }

    private void ApplySelection(ComposerSendMode mode, bool notify)
    {
        _applying = true;
        try { SelectedIndex = mode == ComposerSendMode.ControlEnterSends ? 1 : 0; }
        finally { _applying = false; }
        if (notify) OnPropertyChanged(nameof(SelectedIndex));
    }

    private static Task PublishAsync(Action action) =>
        AvaloniaUiThread.RunAsync(action, CancellationToken.None);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writeGate.Dispose();
    }
}
