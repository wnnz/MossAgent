using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class ProjectAuthorizationLease : ITaskWorkspaceLease
{
    private readonly ProjectAuthorizationMonitor _monitor;
    private readonly ProjectProfile _project;
    private readonly TimeProvider _time;
    private readonly IRootIdentitySession? _roots;
    private readonly IAuthorizationResourceRegistry? _resources;
    private readonly Guid _leaseId = Guid.NewGuid();
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _revocation = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationToken _revocationToken;
    private Task _polling = Task.CompletedTask;
    private Task _heartbeat = Task.CompletedTask;
    private Task _cancelling = Task.CompletedTask;
    private Task? _disposal;
    private bool _revoked;
    private bool _exited;

    internal ProjectAuthorizationLease(ProjectAuthorizationMonitor monitor, ProjectProfile project,
        TimeProvider time, IRootIdentitySession? roots = null,
        IAuthorizationResourceRegistry? resources = null)
    {
        _monitor = monitor;
        _project = project;
        _time = time;
        _roots = roots;
        _resources = resources;
        _revocationToken = _revocation.Token;
    }

    public Guid ProjectId => _project.Id;
    public long Revision => _project.AuthorizationRevision;
    public CancellationToken Revocation => _revocationToken;
    public bool IsRevoked { get { lock (_sync) return _revoked; } }
    internal Task Released => _released.Task;
    internal bool HasExited { get { lock (_sync) return _exited; } }

    internal Task RegisterAsync(CancellationToken cancellationToken) =>
        _resources?.RegisterAsync(_leaseId, _project.Id, _project.AuthorizationRevision, cancellationToken)
        ?? Task.CompletedTask;

    internal void Start()
    {
        lock (_sync)
        {
            if (_disposal is not null || _revoked) return;
            _polling = Task.Run(PollAsync, CancellationToken.None);
            if (_resources is not null) _heartbeat = Task.Run(HeartbeatAsync, CancellationToken.None);
        }
    }

    internal void Revoke()
    {
        lock (_sync)
        {
            if (_revoked || _disposal is not null) return;
            _revoked = true;
            _cancelling = _revocation.CancelAsync();
        }
    }

    private async Task PollAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), _time, _stopping.Token).ConfigureAwait(false);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, deadline.Token);
                await _monitor.ValidateAsync(_project, linked.Token).ConfigureAwait(false);
                if (IsRevoked) return;
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception) { Revoke(); }
    }

    private async Task HeartbeatAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _time, _stopping.Token).ConfigureAwait(false);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2), _time);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, deadline.Token);
                await _resources!.HeartbeatAsync(_leaseId, linked.Token).ConfigureAwait(false);
                if (IsRevoked) return;
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception) { Revoke(); }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_sync)
        {
            if (_disposal is not null) return new(_disposal);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task;
        }
        _ = DisposeOwnedAsync(completion);
        return new(_disposal);
    }

    private async Task DisposeOwnedAsync(TaskCompletionSource completion)
    {
        try
        {
            try
            {
                await _stopping.CancelAsync().ConfigureAwait(false);
                await _polling.ConfigureAwait(false);
                await _heartbeat.ConfigureAwait(false);
                try { await _cancelling.ConfigureAwait(false); }
                catch (AggregateException) { /* Observer failures cannot prevent lease cleanup. */ }
            }
            finally
            {
                var exited = false;
                try
                {
                    await ReleaseRegistrationAsync().ConfigureAwait(false);
                    _stopping.Dispose();
                    _revocation.Dispose();
                    _roots?.Dispose();
                    exited = true;
                }
                finally
                {
                    lock (_sync) _exited = exited;
                    _monitor.Release(this);
                }
            }
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
        finally { _released.TrySetResult(); }
    }

    private async Task ReleaseRegistrationAsync()
    {
        if (_resources is null) return;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2), _time);
        try { await _resources.ReleaseAsync(_leaseId, deadline.Token).ConfigureAwait(false); }
        catch { /* The persisted row remains active and is reported as an unknown resource. */ }
    }
}
