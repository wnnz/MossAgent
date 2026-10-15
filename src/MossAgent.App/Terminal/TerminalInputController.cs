using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Terminal;

public sealed class TerminalInputController : ObservableObject, IDisposable
{
    private readonly IToolExecutor? _executor;
    private readonly ITerminalStateSource? _states;
    private readonly Dictionary<Guid, TerminalInputOperation> _owners = [];
    private ToolExecutionContext? _context;
    private TerminalFrame? _frame;
    private CancellationToken _scope;
    private int _generation;
    private bool _enabled;
    private bool _blocked;
    private bool _disposed;
    private string _notice = string.Empty;

    public TerminalInputController(IToolExecutor? executor, ITerminalStateSource? states)
    {
        _executor = executor;
        _states = states;
        InputCommand = new RelayCommand<TerminalInputRequest>(Request, request => Matches(request) && CanInput && (request?.IsPaste != true || !IsBusy));
        CancelCommand = new RelayCommand(() => Block("TerminalKeysCancelled"), () => IsBusy && !Current!.Token.IsCancellationRequested);
        ResumeCommand = new RelayCommand(() => { _blocked = false; Notice = string.Empty; Notify(); }, () => Eligible && _blocked && !IsBusy);
    }

    public IRelayCommand<TerminalInputRequest> InputCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ResumeCommand { get; }
    public int Generation => _generation;
    private TerminalInputOperation? Current => _context is { } context ? _owners.GetValueOrDefault(context.TaskId) : null;
    public bool IsBusy => !_disposed && Current is not null;
    private bool Eligible => !_disposed && _enabled && !_scope.IsCancellationRequested && _executor is not null && _states is not null
        && _context is { ApprovalPolicy: not ApprovalPolicy.ReadOnly } && _frame is { SessionId: not null, Failed: false };
    public bool CanInput => Eligible && !_blocked && (Current is null || Owns(Current) && !Current.ExclusivePaste && !Current.Token.IsCancellationRequested);
    public string Notice { get => _notice; private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;

    public void Attach(ToolExecutionContext? context, int generation, CancellationToken scope)
    {
        Current?.Cancel();
        _context = context;
        _generation = generation;
        OnPropertyChanged(nameof(Generation));
        _scope = scope;
        _frame = null;
        _enabled = false;
        _blocked = false;
        Notice = Current is null ? string.Empty : InspectorText.Get("TerminalKeysWaitingPrevious");
        Notify();
    }

    public void Observe(TerminalFrame? frame, bool enabled)
    {
        if (_disposed) return;
        var changed = frame?.ViewId != _frame?.ViewId || frame?.SessionId != _frame?.SessionId;
        if (Current?.ExclusivePaste == true && (frame?.Win32InputMode != _frame?.Win32InputMode || frame?.BracketedPaste != _frame?.BracketedPaste))
            Block("TerminalKeysContextChanged");
        if (changed || !enabled) Current?.Cancel();
        if (changed) { _blocked = false; Notice = string.Empty; }
        _frame = frame?.TaskId == _context?.TaskId ? frame : null;
        _enabled = enabled;
        if (!_blocked && Current is { } owner && (!Owns(owner) || owner.Token.IsCancellationRequested))
            Notice = InspectorText.Get("TerminalKeysWaitingPrevious");
        Notify();
    }

    private bool Matches(TerminalInputRequest? request) => request is not null && request.ViewId == _frame?.ViewId && request.SessionId == _frame?.SessionId
        && (request.ScopeGeneration is null || request.ScopeGeneration == _generation);
    private bool Owns(TerminalInputOperation owner) => !_disposed && _generation == owner.Generation && _context?.TaskId == owner.Context.TaskId
        && _frame?.ViewId == owner.ViewId && _frame.SessionId == owner.SessionId;

    private void Request(TerminalInputRequest? request)
    {
        if (!CanInput || !Matches(request) || request?.IsPaste == true && IsBusy) return;
        string? value;
        try { value = TerminalKeyEncoder.Encode(request!, _frame!); }
        catch (ArgumentOutOfRangeException) { Block("TerminalKeysOverflow"); return; }
        catch (ArgumentException) { Block("TerminalPasteInvalid"); return; }
        if (string.IsNullOrEmpty(value)) return;
        var owner = Current;
        if (owner is null)
        {
            if (_owners.Count == 8) { Block("TerminalKeysOverflow"); return; }
            owner = new(_context!, _frame!.ViewId, _frame.SessionId!.Value, _generation, _scope) { ExclusivePaste = request!.IsPaste };
            _owners.Add(owner.Context.TaskId, owner);
            owner.SetDeadline();
            owner.Append(value);
            Notify();
            _ = RunAsync(owner);
        }
        else if (!owner.Append(value)) Block("TerminalKeysOverflow");
    }

    private async Task RunAsync(TerminalInputOperation owner)
    {
        try
        {
            while (owner.HasPending)
            {
                await Task.Delay(15, owner.Token);
                if (!Owns(owner) || !Eligible) return;
                var state = _states!.GetSnapshot(owner.Context.TaskId);
                if (state?.SessionId != owner.SessionId || state.State != TerminalSessionState.Running)
                { Block("TerminalKeysContextChanged"); return; }
                var arguments = JsonSerializer.SerializeToElement(new { input = owner.Take(), sessionId = owner.SessionId.ToString("D") });
                var result = await _executor!.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "terminal.write", arguments), owner.Context, owner.Token);
                owner.CompleteWrite();
                if (!Owns(owner)) return;
                if (owner.Token.IsCancellationRequested) { if (!_blocked) Block("TerminalKeysCancelled"); return; }
                if (!result.IsSuccess)
                {
                    Block(result.ErrorCode is "approval_denied" or "capability_denied" ? "TerminalKeysDenied"
                        : result.ErrorCode == "path_denied" ? "TerminalKeysContextChanged" : "TerminalKeysFailed");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (owner.Token.IsCancellationRequested)
        { if (Owns(owner) && !_blocked) Block("TerminalKeysCancelled"); }
        catch (Exception) { if (Owns(owner)) Block("TerminalKeysFailed"); }
        finally
        {
            _owners.Remove(owner.Context.TaskId);
            owner.Dispose();
            if (!_disposed)
            {
                if (Current is null && Notice == InspectorText.Get("TerminalKeysWaitingPrevious")) Notice = string.Empty;
                Notify();
            }
        }
    }

    private void Block(string label)
    {
        _blocked = true;
        Current?.Cancel();
        Notice = InspectorText.Get(label);
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanInput));
        InputCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var owner in _owners.Values.ToArray()) owner.Cancel();
        _context = null;
        _frame = null;
        Notify();
    }
}
