using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Domain;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class PtyTerminalSessionManager : ITerminalSessionManager, ITerminalStateSource, ITerminalSessionControl, IAuthorizedTerminalSessionManager
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, PtyTerminalOwner> _sessions = [];
    private readonly PtyTerminalStateRegistry _states = new();
    private readonly Func<string, Action<string>, CancellationToken, Task<PtyTerminalSession>> _start;
    private Task? _disposal;
    private bool _disposed;

    public PtyTerminalSessionManager() : this(PtyTerminalSession.StartAsync) { }
    internal PtyTerminalSessionManager(Func<string, Action<string>, CancellationToken, Task<PtyTerminalSession>> start) => _start = start;

    public event EventHandler<TerminalOutputEventArgs>? OutputReceived;
    public event EventHandler<TerminalStateEventArgs>? StateChanged
    { add => _states.StateChanged += value; remove => _states.StateChanged -= value; }
    public TerminalSessionSnapshot? GetSnapshot(Guid taskId) => _states.GetSnapshot(taskId);

    public Task StartAsync(Guid taskId, string workingDirectory, CancellationToken cancellationToken) =>
        StartCoreAsync(taskId, workingDirectory, cancellationToken, null);

    public async Task StartAuthorizedAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.WorkspaceGuard is not ITaskWorkspaceLeaseProvider provider) throw new WorkspaceAuthorizationException();
        var lease = await provider.AcquireAsync(context, cancellationToken).ConfigureAwait(false);
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.Revocation);
            await StartCoreAsync(context.TaskId, context.WorkingDirectory, startup.Token, lease).ConfigureAwait(false);
        }
        finally
        {
            bool retained;
            lock (_sync) retained = ReferenceEquals(_sessions.GetValueOrDefault(context.TaskId)?.Authorization, lease);
            if (!retained) await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task StartCoreAsync(Guid taskId, string workingDirectory, CancellationToken cancellationToken, ITaskWorkspaceLease? authorization)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (!Path.IsPathFullyQualified(workingDirectory)) throw new ArgumentException(nameof(workingDirectory));
        workingDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
        PtyTerminalOwner owner;
        Task<PtyTerminalSession> starting;
        bool created;
        TerminalSessionSnapshot? snapshot = null;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            created = !_sessions.TryGetValue(taskId, out var existing);
            if (created)
            {
                PtyTerminalOwner? captured = null;
                owner = new PtyTerminalOwner(workingDirectory, _start, output => Publish(taskId, captured!, output), authorization);
                captured = owner;
                _sessions.Add(taskId, owner);
                owner.ObserveAuthorization(() => StopRevokedAsync(taskId, owner));
                snapshot = _states.Record(taskId, owner.SessionId, TerminalSessionState.Starting);
            }
            else owner = existing!;
            if (authorization is not null && (owner.Authorization?.ProjectId != authorization.ProjectId
                || owner.Authorization.Revision != authorization.Revision)) throw new WorkspaceAuthorizationException();
            if (!string.Equals(owner.WorkingDirectory, workingDirectory, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(nameof(workingDirectory));
            starting = owner.Start(cancellationToken);
            if (created) owner.Observation = Task.Run(() => ObserveAsync(taskId, owner, starting), CancellationToken.None);
        }
        _states.Publish(snapshot);
        try
        {
            await starting.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            owner.ThrowIfStopping();
            lock (_sync)
            {
                owner.ThrowIfStopping();
                snapshot = _states.Record(taskId, owner.SessionId, TerminalSessionState.Running);
                owner.CompleteHandoff();
            }
            _states.Publish(snapshot);
        }
        catch
        {
            if (created)
            {
                try
                {
                    // Lease revocation can precede delivery to the linked startup token.
                    var stopped = cancellationToken.IsCancellationRequested || owner.IsStopping || owner.Authorization?.IsRevoked == true;
                    await RetireAsync(taskId, owner,
                        stopped ? TerminalSessionState.Stopped : TerminalSessionState.Failed).ConfigureAwait(false);
                }
                finally { await owner.Observation.ConfigureAwait(false); }
            }
            throw;
        }
    }

    public async Task WriteAsync(Guid taskId, string input, CancellationToken cancellationToken)
    {
        var owner = Get(taskId);
        var session = await owner.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        await session.WriteAsync(input, cancellationToken).ConfigureAwait(false);
    }

    public void Resize(Guid taskId, int columns, int rows) => ResizeOwner(taskId, Get(taskId), new(columns, rows));

    public async Task WriteAsync(Guid taskId, Guid? sessionId, string workingDirectory, string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = GetOwned(taskId, sessionId, workingDirectory);
        var session = await owner.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        await session.WriteAsync(input, cancellationToken).ConfigureAwait(false);
    }

    public Task ResizeAsync(Guid taskId, Guid? sessionId, string workingDirectory, TerminalGridSize size, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResizeOwner(taskId, GetOwned(taskId, sessionId, workingDirectory), size, cancellationToken);
        }, cancellationToken);

    private PtyTerminalOwner GetOwned(Guid taskId, Guid? sessionId, string workingDirectory)
    {
        var owner = Get(taskId);
        if (sessionId is not null && owner.SessionId != sessionId
            || !string.Equals(owner.WorkingDirectory, Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Terminal session ownership changed.");
        return owner;
    }

    private void ResizeOwner(Guid taskId, PtyTerminalOwner owner, TerminalGridSize size, CancellationToken cancellationToken = default)
    {
        var snapshot = owner.Resize(size, () =>
        {
            lock (_sync) return !_disposed && _sessions.GetValueOrDefault(taskId) == owner && !owner.IsStopping
                ? _states.Record(taskId, owner.SessionId, TerminalSessionState.Running, grid: size) : null;
        }, cancellationToken);
        _states.Publish(snapshot);
    }

    public async Task StopAsync(Guid taskId)
    {
        PtyTerminalOwner? owner;
        lock (_sync) _sessions.TryGetValue(taskId, out owner);
        if (owner is not null) await StopAndObserveAsync(taskId, owner).ConfigureAwait(false);
    }

    private async Task StopRevokedAsync(Guid taskId, PtyTerminalOwner owner)
    {
        try { await StopAndObserveAsync(taskId, owner).ConfigureAwait(false); }
        catch (Exception) { /* Retirement retains CleanupFailed in the task-owned state registry. */ }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        KeyValuePair<Guid, PtyTerminalOwner>[] owners;
        lock (_sync)
        {
            if (_disposal is not null) return new ValueTask(_disposal);
            _disposed = true;
            owners = _sessions.ToArray();
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task;
        }
        _ = DisposeOwnedAsync(owners, completion);
        return new ValueTask(completion.Task);
    }

    private async Task DisposeOwnedAsync(KeyValuePair<Guid, PtyTerminalOwner>[] owners, TaskCompletionSource completion)
    {
        try
        {
            await Task.WhenAll(owners.Select(pair => StopAndObserveAsync(pair.Key, pair.Value))).ConfigureAwait(false);
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
    }

    private async Task ObserveAsync(Guid taskId, PtyTerminalOwner owner, Task<PtyTerminalSession> starting)
    {
        PtyTerminalSession session;
        try { session = await starting.ConfigureAwait(false); }
        catch (Exception) { return; }
        await owner.Handoff.ConfigureAwait(false);
        try
        {
            var exitCode = await session.Termination.ConfigureAwait(false);
            await RetireAsync(taskId, owner, TerminalSessionState.Exited, exitCode).ConfigureAwait(false);
        }
        catch (Exception)
        {
            if (owner.IsStopping) return;
            try { await RetireAsync(taskId, owner, TerminalSessionState.Failed).ConfigureAwait(false); }
            catch (Exception) { }
        }
    }

    private async Task StopAndObserveAsync(Guid taskId, PtyTerminalOwner owner)
    {
        try { await RetireAsync(taskId, owner).ConfigureAwait(false); }
        finally { await owner.Observation.ConfigureAwait(false); }
    }

    private async Task RetireAsync(Guid taskId, PtyTerminalOwner owner, TerminalSessionState finalState = TerminalSessionState.Stopped, int? exitCode = null)
    {
        TerminalSessionSnapshot? snapshot;
        lock (_sync)
        {
            if (!_sessions.TryGetValue(taskId, out var current) || !ReferenceEquals(current, owner)) return;
            owner.BeginRetirement(finalState, exitCode);
            snapshot = _states.Record(taskId, owner.SessionId, TerminalSessionState.Stopping, owner.ExitCode);
        }
        var stopping = owner.StopAsync();
        _states.Publish(snapshot);
        try { await stopping.ConfigureAwait(false); }
        catch
        {
            lock (_sync) snapshot = _states.Record(taskId, owner.SessionId, TerminalSessionState.CleanupFailed, owner.ExitCode);
            _states.Publish(snapshot);
            throw;
        }
        lock (_sync)
        {
            snapshot = null;
            if (_sessions.TryGetValue(taskId, out var current) && ReferenceEquals(current, owner))
            {
                _sessions.Remove(taskId);
                snapshot = _states.Record(taskId, owner.SessionId, owner.FinalState, owner.ExitCode);
            }
        }
        _states.Publish(snapshot);
    }

    private PtyTerminalOwner Get(Guid taskId)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _sessions.TryGetValue(taskId, out var owner)
                ? owner : throw new InvalidOperationException("当前任务没有活动终端。");
        }
    }

    private void Publish(Guid taskId, PtyTerminalOwner owner, string output)
    {
        lock (_sync)
            if (_disposed || owner.IsStopping || !_sessions.TryGetValue(taskId, out var current) || !ReferenceEquals(current, owner)) return;
        var args = new TerminalOutputEventArgs(taskId, output, owner.SessionId);
        foreach (var subscriber in OutputReceived?.GetInvocationList() ?? [])
        {
            try { ((EventHandler<TerminalOutputEventArgs>)subscriber)(this, args); }
            catch (Exception) { }
        }
    }
}
