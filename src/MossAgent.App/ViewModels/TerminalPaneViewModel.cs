using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.App.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.ViewModels;

public sealed class TerminalPaneViewModel : ObservableObject, IDisposable
{
    private readonly ITerminalSessionManager _terminals;
    private readonly IToolExecutor? _tools;
    private readonly TerminalOutputBuffer _outputBuffer;
    private readonly ITerminalStateSource? _states;
    private readonly TerminalStateBuffer _stateBuffer;
    private readonly TerminalScreenProjection _screen;
    private TerminalFrame? _frame;
    private TerminalSessionSnapshot? _snapshot;
    private bool _stateAvailable = true;
    private bool _hasOperationNotice;
    private readonly Dictionary<Guid, TerminalPaneOperation> _operations = [];
    private Guid? _taskId;
    private ToolExecutionContext? _context;
    private CancellationTokenSource _scope = new();
    private string _output = string.Empty;
    private string _input = string.Empty;
    private string _status = InspectorText.Get("SelectTask");
    private bool _hasTruncatedOutput;
    private bool _disposed;
    private int _generation;

    public TerminalPaneViewModel(ITerminalSessionManager terminals, IToolExecutor? tools = null)
    {
        _terminals = terminals;
        _tools = tools;
        _outputBuffer = new TerminalOutputBuffer(action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background), ApplyOutput);
        _stateBuffer = new TerminalStateBuffer(action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background), ApplyState);
        _states = terminals as ITerminalStateSource;
        Resize = new(tools, _states);
        Keyboard = new(tools, _states);
        _screen = new(_states is not null, action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background), ApplyFrame);
        if (_states is not null) _states.StateChanged += OnStateChanged;
        _terminals.OutputReceived += OnOutputReceived;
        StartCommand = new AsyncRelayCommand(() => ExecuteAsync("terminal.start", "TerminalStarted"), CanStart, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        StopCommand = new AsyncRelayCommand(() => ExecuteAsync("terminal.stop", "TerminalStopped"), CanStop, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelOperationCommand = new RelayCommand(CancelOperation, () => CanCancelOperation);
        Keyboard.PropertyChanged += OnKeyboardChanged;
    }

    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand SendCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public TerminalResizeController Resize { get; }
    public TerminalInputController Keyboard { get; }
    private TerminalPaneOperation? CurrentOperation => _taskId is { } task && _operations.TryGetValue(task, out var operation) ? operation : null;
    public bool HasOutput => Frame?.HasOutput == true || Output.Length > 0;
    public TerminalFrame? Frame
    {
        get => _frame;
        private set
        {
            if (!SetProperty(ref _frame, value)) return;
            OnPropertyChanged(nameof(HasOutput));
            OnPropertyChanged(nameof(HasRenderingFailure));
            ObserveInteraction();
        }
    }
    public bool HasRenderingFailure => Frame?.Failed == true;
    public bool IsBusy => !_disposed && CurrentOperation is not null;
    public bool CanCancelOperation => IsBusy && !CurrentOperation!.Token.IsCancellationRequested;
    public string CancelOperationLabel => InspectorText.Get(CanCancelOperation ? "TerminalCancel" : "TerminalCancellingButton");
    public bool CanEditInput => !_disposed && _context is not null && _tools is not null && !IsBusy;
    public bool HasTruncatedOutput { get => _hasTruncatedOutput; private set => SetProperty(ref _hasTruncatedOutput, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasSessionState => _context is not null && _states is not null;
    public bool HasOperationNotice => _states is null || _snapshot is null || _hasOperationNotice;
    public string EmptyTitle => InspectorText.Get(_snapshot is null ? "TerminalIdle" : "TerminalNoOutput");
    public string SessionStatus => !_stateAvailable ? InspectorText.Get("TerminalStateUnavailable") : _snapshot is null
        ? InspectorText.Get("TerminalSessionIdle") : _snapshot.State switch
        {
            TerminalSessionState.Starting => InspectorText.Get("TerminalSessionStarting"),
            TerminalSessionState.Running => InspectorText.Get("TerminalSessionRunning"),
            TerminalSessionState.Stopping => InspectorText.Get("TerminalSessionStopping"),
            TerminalSessionState.Stopped => InspectorText.Get("TerminalStopped"),
            TerminalSessionState.Exited when _snapshot.ExitCode is { } exitCode => InspectorText.Format("TerminalSessionExited", exitCode),
            TerminalSessionState.CleanupFailed => InspectorText.Get("TerminalSessionCleanupFailed"),
            _ => InspectorText.Get("TerminalSessionFailed")
        };
    public string Output { get => _output; private set { if (SetProperty(ref _output, value)) OnPropertyChanged(nameof(HasOutput)); } }
    public string Input { get => _input; set { if (SetProperty(ref _input, value)) SendCommand.NotifyCanExecuteChanged(); } }

    public void AttachTask(Guid? taskId, string? workingDirectory, ToolExecutionContext? context = null)
    {
        if (_disposed) return;
        _generation++;
        try { _scope.Cancel(); }
        catch (AggregateException) { }
        _scope.Dispose();
        _scope = new();
        _taskId = taskId;
        _context = context is not null && context.TaskId == taskId && context.WorkingDirectory == workingDirectory ? context : null;
        _outputBuffer.Attach(_context?.TaskId, _generation);
        _stateBuffer.Attach(_context?.TaskId, _generation);
        _snapshot = null;
        _hasOperationNotice = false;
        ReadState();
        Frame = _screen.Attach(_context, _snapshot, _generation);
        Resize.Attach(_context, Frame?.SessionId == _snapshot?.SessionId ? _snapshot : null, _generation, _scope.Token);
        Keyboard.Attach(_context, _generation, _scope.Token);
        Output = string.Empty;
        Input = string.Empty;
        HasTruncatedOutput = false;
        Status = InspectorText.Get(taskId is null ? "SelectTask" : IsBusy ? "TerminalCancelling" : "TerminalHint");
        NotifyCommands();
    }

    private bool CanExecute() => !_disposed && !IsBusy && _context is not null && _tools is not null && _stateAvailable;
    private bool CanStart() => CanExecute() && (_states is null || _snapshot is null || _snapshot.State is TerminalSessionState.Stopped or TerminalSessionState.Exited or TerminalSessionState.Failed);
    private bool CanSend() => CanExecute() && !Keyboard.IsBusy && !string.IsNullOrWhiteSpace(Input) && (_states is null || _snapshot?.State == TerminalSessionState.Running);
    private bool CanStop() => CanExecute() && (_states is null || _snapshot?.State is TerminalSessionState.Starting or TerminalSessionState.Running);
    private bool CanExecuteTool(string tool) => tool switch { "terminal.start" => CanStart(), "terminal.write" => CanSend(), "terminal.stop" => CanStop(), _ => false };
    private Task SendAsync() => ExecuteAsync("terminal.write", "TerminalSent", Input);

    private async Task ExecuteAsync(string tool, string successLabel, string? input = null)
    {
        ReadState();
        if (!CanExecuteTool(tool) || (tool == "terminal.write" && string.IsNullOrWhiteSpace(input))) { NotifyCommands(); return; }
        using var operation = new TerminalPaneOperation(_context!, _generation, _scope.Token);
        _operations.Add(operation.Context.TaskId, operation);
        var value = input is null ? null : input.EndsWith('\r') || input.EndsWith('\n') ? input : input + "\r";
        var arguments = value is null ? JsonSerializer.SerializeToElement(new { }) : _snapshot is { } snapshot
            ? JsonSerializer.SerializeToElement(new { input = value, sessionId = snapshot.SessionId.ToString("D") }) : JsonSerializer.SerializeToElement(new { input = value });
        _hasOperationNotice = true;
        Status = InspectorText.Get(tool switch { "terminal.start" => "TerminalStarting", "terminal.stop" => "TerminalStopping", _ => "TerminalSending" });
        NotifyCommands();
        try
        {
            var result = await _tools!.ExecuteAsync(new ToolRequest(Guid.NewGuid().ToString("N"), tool, arguments), operation.Context, operation.Token);
            if (_disposed || operation.Generation != _generation) return;
            _hasOperationNotice = !result.IsSuccess || operation.Token.IsCancellationRequested || input is not null || _states is null;
            Status = InspectorText.Get(result.IsSuccess ? operation.Token.IsCancellationRequested ? "TerminalCancellationReturned" : successLabel : FailureLabel(result.ErrorCode));
            if (result.IsSuccess && input is not null && Input == input) Input = string.Empty;
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        { if (!_disposed && operation.Generation == _generation) Status = InspectorText.Get("TerminalCancelled"); }
        catch (Exception) { if (!_disposed && operation.Generation == _generation) Status = InspectorText.Get("TerminalFailed"); }
        finally
        {
            _operations.Remove(operation.Context.TaskId);
            if (!_disposed && _taskId == operation.Context.TaskId) ReadState();
            if (!_disposed && _taskId == operation.Context.TaskId && operation.Generation != _generation) Status = InspectorText.Get("TerminalContextChanged");
            NotifyCommands();
        }
    }

    private static string FailureLabel(string? error) => error switch
    {
        "tool_cancelled" => "TerminalCancelled",
        "tool_timeout" => "TerminalTimedOut",
        "approval_denied" or "capability_denied" => "TerminalPermissionDenied",
        "path_denied" or "workspace_denied" => "TerminalPathDenied",
        _ => "TerminalFailed"
    };

    private void CancelOperation()
    {
        if (!CanCancelOperation) return;
        var operation = CurrentOperation!;
        operation.Cancel();
        if (ReferenceEquals(CurrentOperation, operation) && operation.Generation == _generation) Status = InspectorText.Get("TerminalCancelling");
        NotifyCommands();
    }

    private void OnOutputReceived(object? sender, TerminalOutputEventArgs eventArgs)
    {
        TerminalSessionSnapshot? snapshot = null;
        try { snapshot = _states?.GetSnapshot(eventArgs.TaskId); }
        catch (Exception) { }
        if (_screen.Append(eventArgs, snapshot)) _outputBuffer.Append(eventArgs.TaskId, eventArgs.Output);
    }

    private void OnStateChanged(object? sender, TerminalStateEventArgs eventArgs)
    {
        _screen.ApplyState(eventArgs.Snapshot);
        _stateBuffer.Append(eventArgs.Snapshot);
    }

    private void ApplyFrame(int generation, TerminalFrame? frame)
    {
        if (_disposed || generation != _generation || frame?.TaskId != _context?.TaskId
            || frame is not null && !_screen.IsCurrent(frame)) return;
        Frame = frame;
        HasTruncatedOutput |= frame?.HistoryLimited == true;
    }

    private void ReadState()
    {
        _stateAvailable = true;
        if (_states is null || _context is null) return;
        try
        {
            var snapshot = _states.GetSnapshot(_context.TaskId);
            if (snapshot is not null && snapshot.TaskId == _context.TaskId && (_snapshot is null || snapshot.Revision > _snapshot.Revision)) _snapshot = snapshot;
        }
        catch (Exception) { _stateAvailable = false; }
    }

    private void ApplyState(int generation, TerminalSessionSnapshot snapshot)
    {
        if (_disposed || generation != _generation || snapshot.TaskId != _context?.TaskId) return;
        if (_snapshot is not null && (snapshot.Revision < _snapshot.Revision || snapshot.Revision == _snapshot.Revision && (snapshot != _snapshot || _stateAvailable))) return;
        _snapshot = snapshot;
        _stateAvailable = true;
        NotifyCommands();
    }

    private void ApplyOutput(int generation, string text, bool truncated)
    {
        if (_disposed || generation != _generation) return;
        var combined = Output + text;
        var overflow = combined.Length - TerminalOutputBuffer.MaximumCharacters;
        if (overflow > 0 && char.IsLowSurrogate(combined[overflow])) overflow++;
        Output = overflow > 0 ? combined[overflow..] : combined;
        if (!_disposed && generation == _generation) HasTruncatedOutput |= truncated || overflow > 0;
    }

    private void NotifyCommands()
    {
        ObserveInteraction();
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanCancelOperation));
        OnPropertyChanged(nameof(CancelOperationLabel));
        OnPropertyChanged(nameof(CanEditInput));
        OnPropertyChanged(nameof(HasSessionState));
        OnPropertyChanged(nameof(HasOperationNotice));
        OnPropertyChanged(nameof(SessionStatus));
        OnPropertyChanged(nameof(EmptyTitle));
        StartCommand.NotifyCanExecuteChanged();
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
    }

    private void ObserveInteraction()
    {
        var current = _stateAvailable && Frame?.SessionId == _snapshot?.SessionId;
        Resize.Observe(current ? _snapshot : null);
        Keyboard.Observe(current ? Frame : null, current && _snapshot?.State == TerminalSessionState.Running && !IsBusy);
    }

    private void OnKeyboardChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    { if (eventArgs.PropertyName == nameof(Keyboard.IsBusy)) SendCommand.NotifyCanExecuteChanged(); }

    public void Dispose()
    {
        if (_disposed) return;
        AttachTask(null, null);
        _disposed = true;
        foreach (var operation in _operations.Values.ToArray()) operation.Cancel();
        _outputBuffer.Dispose();
        _stateBuffer.Dispose();
        _screen.Dispose();
        Resize.Dispose();
        Keyboard.PropertyChanged -= OnKeyboardChanged;
        Keyboard.Dispose();
        _scope.Cancel();
        _scope.Dispose();
        _terminals.OutputReceived -= OnOutputReceived;
        if (_states is not null) _states.StateChanged -= OnStateChanged;
        NotifyCommands();
    }
}
