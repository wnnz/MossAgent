using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using MossAgent.App.Resources;
using MossAgent.App.Terminal;

namespace MossAgent.App.Controls;

public sealed class TerminalClipboardCommands : ObservableObject
{
    private readonly TerminalSelection _selection;
    private readonly Func<bool> _available;
    private readonly Func<string, Task> _copy;
    private readonly Func<Task<string?>> _read;
    private TerminalFrame? _frame;
    private ICommand? _input;
    private CancellationTokenSource? _owner;
    private TerminalInputRequest? _pending;
    private int _generation;
    private int _scopeGeneration;
    private string _notice = string.Empty;

    public TerminalClipboardCommands(TerminalSelection selection, Func<bool> available, Func<string, Task> copy, Func<Task<string?>> read)
    {
        _selection = selection;
        _available = available;
        _copy = copy;
        _read = read;
        CopyCommand = new AsyncRelayCommand(CopyAsync, () => Ready && _selection.HasSelection && !IsBusy);
        PasteCommand = new AsyncRelayCommand(PasteAsync, () => CanPaste);
        SelectAllCommand = new RelayCommand(_selection.SelectAll, () => Ready && !IsBusy);
        ConfirmPasteCommand = new RelayCommand(ConfirmPaste, () => HasPendingPaste && Ready && _input?.CanExecute(_pending) == true);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy || HasPendingPaste);
    }

    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand PasteCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ConfirmPasteCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool IsBusy => _owner is not null;
    public bool HasPendingPaste => _pending is not null;
    private bool Ready => _available() && _frame is { Failed: false, SessionId: not null };
    private bool CanPaste => Ready && !IsBusy && !HasPendingPaste && _input?.CanExecute(Request(string.Empty)) == true;
    public string Notice { get => _notice; private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;

    public void Observe(TerminalFrame? frame, ICommand? input, int scopeGeneration)
    {
        if (frame?.ViewId != _frame?.ViewId || frame?.TaskId != _frame?.TaskId || frame?.SessionId != _frame?.SessionId
            || frame?.AlternateScreen != _frame?.AlternateScreen || frame?.Win32InputMode != _frame?.Win32InputMode
            || frame?.BracketedPaste != _frame?.BracketedPaste || !ReferenceEquals(input, _input) || scopeGeneration != _scopeGeneration)
            Invalidate();
        if (!ReferenceEquals(input, _input))
        {
            if (_input is not null) _input.CanExecuteChanged -= HandleEligibilityChanged;
            _input = input;
            if (input is not null) input.CanExecuteChanged += HandleEligibilityChanged;
        }
        _scopeGeneration = scopeGeneration;
        _frame = frame;
        _selection.Observe(frame);
        RefreshAvailability();
    }

    private void HandleEligibilityChanged(object? sender, EventArgs args) => RefreshAvailability();
    public void RefreshSelection() { Invalidate(); Notify(); }
    public void RefreshAvailability()
    {
        if (!Ready) Invalidate();
        else if (_owner?.IsCancellationRequested == true && Notice.Length == 0) Notice = InspectorText.Get("TerminalClipboardWaitingPrevious");
        Notify();
    }

    private TerminalInputRequest Request(string text) => new(_frame!.ViewId, _frame.SessionId!.Value, text, IsPaste: true, ScopeGeneration: _scopeGeneration);
    private void Invalidate()
    {
        _generation++;
        _owner?.Cancel();
        _pending = null;
        Notice = string.Empty;
    }

    private async Task CopyAsync(CancellationToken token)
    {
        if (!Ready || !_selection.HasSelection || IsBusy) return;
        string? payload;
        try { payload = _selection.GetText(); }
        catch (ArgumentOutOfRangeException) { Notice = InspectorText.Get("TerminalClipboardTooLarge"); return; }
        if (string.IsNullOrEmpty(payload)) return;
        await RunAsync(async cancellation => { cancellation.ThrowIfCancellationRequested(); await _copy(payload); }, "TerminalSelectionCopied", token);
    }

    private async Task PasteAsync(CancellationToken token)
    {
        if (!CanPaste) return;
        var generation = _generation;
        var frame = _frame!;
        await RunAsync(async cancellation =>
        {
            var text = await _read();
            cancellation.ThrowIfCancellationRequested();
            if (generation != _generation || !Ready) return;
            if (string.IsNullOrEmpty(text)) { Notice = string.Empty; return; }
            TerminalPasteEncoder.Encode(text, frame);
            var request = Request(text);
            if (_input?.CanExecute(request) != true) { Notice = InspectorText.Get("TerminalClipboardInputUnavailable"); return; }
            if (text.Contains('\n') || text.Contains('\r'))
            {
                _pending = request;
                Notice = InspectorText.Get("TerminalPasteConfirmHint");
            }
            else { Notice = string.Empty; _input.Execute(request); }
        }, null, token);
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, string? success, CancellationToken token)
    {
        var generation = _generation;
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(token);
        owner.CancelAfter(TimeSpan.FromSeconds(30));
        _owner = owner;
        using var registration = owner.Token.Register(() => Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_owner, owner) && generation == _generation)
            { Notice = InspectorText.Get("TerminalClipboardCancelled"); Notify(); }
        }));
        Notice = InspectorText.Get(success is null ? "TerminalClipboardReading" : "TerminalClipboardCopying");
        Notify();
        try
        {
            owner.Token.ThrowIfCancellationRequested();
            await action(owner.Token);
            owner.Token.ThrowIfCancellationRequested();
            if (generation == _generation && Ready && success is not null) Notice = InspectorText.Get(success);
        }
        catch (OperationCanceledException) { if (generation == _generation) Notice = InspectorText.Get("TerminalClipboardCancelled"); }
        catch (ArgumentOutOfRangeException) { if (generation == _generation) Notice = InspectorText.Get("TerminalClipboardTooLarge"); }
        catch (ArgumentException) { if (generation == _generation) Notice = InspectorText.Get("TerminalPasteInvalid"); }
        catch (Exception) { if (generation == _generation) Notice = InspectorText.Get("TerminalClipboardFailed"); }
        finally
        {
            _owner = null;
            if (Notice == InspectorText.Get("TerminalClipboardWaitingPrevious")) Notice = string.Empty;
            Notify();
        }
    }

    private void ConfirmPaste()
    {
        if (!ConfirmPasteCommand.CanExecute(null)) return;
        var request = _pending!;
        _pending = null;
        Notice = string.Empty;
        _input!.Execute(request);
        Notify();
    }

    private void Cancel()
    {
        _owner?.Cancel();
        _pending = null;
        Notice = InspectorText.Get("TerminalClipboardCancelled");
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasPendingPaste));
        CopyCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        ConfirmPasteCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
