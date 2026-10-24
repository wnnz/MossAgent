using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Platform.Windows.Terminal;

internal sealed class PtyTerminalOwner
{
    private readonly object _sync = new();
    private readonly object _resizeSync = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationToken _token;
    private readonly Func<string, Action<string>, CancellationToken, Task<PtyTerminalSession>> _factory;
    private readonly Action<string> _output;
    private Task<PtyTerminalSession>? _starting;
    private Task? _stopping;
    private PtyTerminalSession? _session;
    private readonly TaskCompletionSource _handoff = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TerminalSessionState? _finalState;
    private CancellationTokenRegistration _authorizationRegistration;

    public PtyTerminalOwner(string workingDirectory,
        Func<string, Action<string>, CancellationToken, Task<PtyTerminalSession>> factory, Action<string> output,
        ITaskWorkspaceLease? authorization = null)
    {
        WorkingDirectory = workingDirectory;
        _factory = factory;
        _output = output;
        _token = _shutdown.Token;
        Authorization = authorization;
    }

    public string WorkingDirectory { get; }
    public ITaskWorkspaceLease? Authorization { get; }

    public void ObserveAuthorization(Func<Task> stop)
    {
        if (Authorization is not null)
            _authorizationRegistration = Authorization.Revocation.Register(() => _ = Task.Run(stop, CancellationToken.None));
    }
    public Guid SessionId { get; } = Guid.NewGuid();
    public Task Observation { get; set; } = Task.CompletedTask;
    public Task Handoff => _handoff.Task;
    public TerminalSessionState FinalState { get { lock (_sync) return _finalState ?? TerminalSessionState.Stopped; } }
    public int? ExitCode { get; private set; }
    public bool IsStopping { get { lock (_sync) return _stopping is not null || _finalState.HasValue; } }
    public void CompleteHandoff() => _handoff.TrySetResult();

    public void BeginRetirement(TerminalSessionState state, int? exitCode)
    {
        lock (_sync)
        {
            if (_finalState.HasValue) return;
            _finalState = state;
            ExitCode = exitCode;
        }
    }
    public void ThrowIfStopping()
    {
        if (Authorization?.IsRevoked == true) throw new MossAgent.Domain.WorkspaceAuthorizationException();
        if (IsStopping) throw new OperationCanceledException(_token);
    }

    public Task<PtyTerminalSession> Start(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ThrowIfStopping();
            return _starting ??= Task.Run(() => CreateAsync(cancellationToken), CancellationToken.None);
        }
    }

    private async Task<PtyTerminalSession> CreateAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _token);
        linked.Token.ThrowIfCancellationRequested();
        var session = await _factory(WorkingDirectory, _output, linked.Token).ConfigureAwait(false);
        lock (_sync) _session = session;
        if (linked.IsCancellationRequested)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
        }
        return session;
    }

    public async Task<PtyTerminalSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        Task<PtyTerminalSession> starting;
        lock (_sync) starting = _starting ?? throw new InvalidOperationException();
        var session = await starting.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfStopping();
        return session;
    }

    public PtyTerminalSession GetReadySession()
    {
        lock (_sync)
        {
            ThrowIfStopping();
            return _starting is { IsCompletedSuccessfully: true } ? _starting.Result : throw new InvalidOperationException();
        }
    }

    public TerminalSessionSnapshot? Resize(TerminalGridSize size, Func<TerminalSessionSnapshot?> commit, CancellationToken cancellationToken)
    {
        lock (_resizeSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetReadySession().Resize(size.Columns, size.Rows);
            return commit();
        }
    }

    public Task StopAsync()
    {
        TaskCompletionSource completion;
        Task<PtyTerminalSession>? starting;
        lock (_sync)
        {
            if (_stopping is not null) return _stopping;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopping = completion.Task;
            starting = _starting;
            CompleteHandoff();
        }
        try { _shutdown.Cancel(); }
        catch (AggregateException) { }
        _ = StopOwnedAsync(starting, completion);
        return completion.Task;
    }

    private async Task StopOwnedAsync(Task<PtyTerminalSession>? starting, TaskCompletionSource completion)
    {
        try
        {
            if (starting is not null) await ((Task)starting).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            PtyTerminalSession? session;
            lock (_sync) session = _session;
            if (session is not null) await Task.Run(() =>
            {
                lock (_resizeSync) return session.DisposeAsync().AsTask();
            }, CancellationToken.None).ConfigureAwait(false);
            if (Authorization is not null) await Authorization.DisposeAsync().ConfigureAwait(false);
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
        finally
        {
            _authorizationRegistration.Dispose();
            _shutdown.Dispose();
        }
    }
}
