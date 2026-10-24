using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Terminal;

public sealed class TerminalResizeController : ObservableObject, IDisposable
{
    private readonly IToolExecutor? _executor;
    private readonly ITerminalStateSource? _states;
    private readonly Dictionary<Guid, TerminalResizeOperation> _owners = [];
    private ToolExecutionContext? _context;
    private TerminalSessionSnapshot? _snapshot;
    private TerminalGridSize? _wanted;
    private CancellationToken _scope;
    private int _generation;
    private bool _blocked;
    private bool _disposed;
    private string _notice = string.Empty;

    public TerminalResizeController(IToolExecutor? executor, ITerminalStateSource? states)
    {
        _executor = executor;
        _states = states;
        ResizeCommand = new RelayCommand<TerminalGridSize>(Request, size => size is not null && CanResize);
        ApplyCommand = new RelayCommand(() => { _blocked = false; StartLatest(true); }, () => CanApplySize);
        RetryCommand = new RelayCommand(() => { _blocked = false; StartLatest(true); }, () => CanResize && _blocked && !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => Current is { } owner && !owner.Token.IsCancellationRequested);
    }

    public IRelayCommand<TerminalGridSize> ResizeCommand { get; }
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand RetryCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool RequiresExplicitResize => _context?.ApprovalPolicy == ApprovalPolicy.AskEveryTime;
    public bool CanApplySize => CanResize && !IsBusy && _wanted is not null && _wanted != _snapshot?.Grid;
    private TerminalResizeOperation? Current => _context is { } context ? _owners.GetValueOrDefault(context.TaskId) : null;
    public bool IsBusy => !_disposed && Current is not null;
    public bool CanResize => !_disposed && !_scope.IsCancellationRequested && _executor is not null && _states is not null && _context?.ApprovalPolicy != ApprovalPolicy.ReadOnly
        && _snapshot is { State: TerminalSessionState.Running, Grid: not null } && _snapshot.TaskId == _context?.TaskId;
    public string Notice { get => _notice; private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;

    public void Attach(ToolExecutionContext? context, TerminalSessionSnapshot? snapshot, int generation, CancellationToken scope)
    {
        Current?.Cancel();
        _context = context;
        _snapshot = snapshot;
        _generation = generation;
        _scope = scope;
        _wanted = null;
        _blocked = false;
        Notice = string.Empty;
        Notify();
    }

    public void Observe(TerminalSessionSnapshot? snapshot)
    {
        if (_disposed || snapshot == _snapshot) return;
        if (snapshot?.SessionId != _snapshot?.SessionId || snapshot?.State != TerminalSessionState.Running)
        {
            Current?.Cancel();
            _blocked = false;
        }
        _snapshot = snapshot;
        Notify();
        StartLatest();
    }

    private void Request(TerminalGridSize? size)
    {
        if (!CanResize || size is null || size == _wanted) return;
        _wanted = size;
        Notify();
        StartLatest();
    }

    private void StartLatest(bool explicitlyRequested = false)
    {
        if (!CanResize || _blocked || _wanted is null || _snapshot!.Grid == _wanted || Current is not null || _owners.Count == 8) return;
        if (RequiresExplicitResize && !explicitlyRequested) return;
        var owner = new TerminalResizeOperation(_context!, _snapshot.SessionId, _generation, _scope);
        owner.SetDeadline();
        _owners.Add(owner.Context.TaskId, owner);
        Notify();
        _ = RunAsync(owner);
    }

    private bool Owns(TerminalResizeOperation owner) => !_disposed && _generation == owner.Generation
        && _context?.TaskId == owner.Context.TaskId && _snapshot?.SessionId == owner.SessionId;

    private async Task RunAsync(TerminalResizeOperation owner)
    {
        var success = false;
        try
        {
            await Task.Delay(150, owner.Token);
            if (!Owns(owner) || !CanResize) return;
            var state = _states?.GetSnapshot(owner.Context.TaskId);
            if (state?.SessionId != owner.SessionId || state.State != TerminalSessionState.Running) return;
            var size = _wanted!;
            if (state.Grid == size) { _wanted = null; success = true; return; }
            var arguments = JsonSerializer.SerializeToElement(new { columns = size.Columns, rows = size.Rows, sessionId = owner.SessionId.ToString("D") });
            var result = await _executor!.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "terminal.resize", arguments), owner.Context, owner.Token);
            success = result.IsSuccess && !owner.Token.IsCancellationRequested;
            if (!Owns(owner)) return;
            _blocked = !success;
            Notice = success ? string.Empty : InspectorText.Get(result.ErrorCode is "approval_denied" or "capability_denied" ? "TerminalResizeDenied" : "TerminalResizeFailed");
            if (success && _wanted == size) _wanted = null;
        }
        catch (OperationCanceledException) when (owner.Token.IsCancellationRequested)
        {
            if (Owns(owner)) { _blocked = true; Notice = InspectorText.Get("TerminalResizeCancelled"); }
        }
        catch (Exception)
        {
            if (Owns(owner)) { _blocked = true; Notice = InspectorText.Get("TerminalResizeFailed"); }
        }
        finally
        {
            _owners.Remove(owner.Context.TaskId);
            owner.Dispose();
            if (!_disposed) { Notify(); if (success || !Owns(owner)) StartLatest(); }
        }
    }

    private void Cancel()
    {
        Current?.Cancel();
        Notice = InspectorText.Get("TerminalResizeCancelling");
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanResize));
        OnPropertyChanged(nameof(RequiresExplicitResize));
        OnPropertyChanged(nameof(CanApplySize));
        ResizeCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var owner in _owners.Values.ToArray()) owner.Cancel();
        _context = null;
        _snapshot = null;
        _wanted = null;
        Notify();
    }
}
