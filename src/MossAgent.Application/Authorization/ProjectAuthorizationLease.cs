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
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _revocation = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationToken _revocationToken;
    private Task _polling = Task.CompletedTask;
    private Task _cancelling = Task.CompletedTask;
    private Task? _disposal;
    private bool _revoked;
    private bool _releaseFailed;

    internal ProjectAuthorizationLease(ProjectAuthorizationMonitor monitor, ProjectProfile project, TimeProvider time, IRootIdentitySession? roots = null)
    {
        _monitor = monitor;
        _project = project;
        _time = time;
        _roots = roots;
        _revocationToken = _revocation.Token;
    }

    public Guid ProjectId => _project.Id;
    public long Revision => _project.AuthorizationRevision;
    public CancellationToken Revocation => _revocationToken;
    public bool IsRevoked { get { lock (_sync) return _revoked; } }
    internal bool ReleaseFailed { get { lock (_sync) return _releaseFailed; } }

    internal void Start()
    {
        lock (_sync)
        {
            if (_disposal is not null || _revoked) return;
            _polling = Task.Run(PollAsync, CancellationToken.None);
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
        _monitor.NotifyLeaseRevoked(this);
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
        var released = false;
        using var deadline = new CancellationTokenSource(ProjectAuthorizationMonitor.DrainTimeout, _time);
        try
        {
            await _stopping.CancelAsync().WaitAsync(deadline.Token).ConfigureAwait(false);
            await _polling.WaitAsync(deadline.Token).ConfigureAwait(false);
            try { await _cancelling.WaitAsync(deadline.Token).ConfigureAwait(false); }
            catch (AggregateException) { /* Observer failures cannot prevent lease cleanup. */ }
            if (_roots is not null) await ReleaseRootsAsync(deadline.Token).ConfigureAwait(false);
            _monitor.Release(this);
            released = true;
            completion.SetResult();
        }
        catch (Exception exception)
        {
            if (!released)
            {
                MarkReleaseFailed();
                if (deadline.IsCancellationRequested)
                    exception = new TimeoutException("The revocation drain exceeded its deadline.", exception);
            }
            completion.SetException(exception);
        }
        finally
        {
            _stopping.Dispose();
            _revocation.Dispose();
        }
    }

    /// <summary>Releases the root session under the shared drain deadline. A failed first
    /// attempt gets exactly one retry after a bounded settle delay — transient release
    /// failures (handle still closing, I/O flush) recover instead of leaving CleanupUnknown;
    /// persistent failures still throw and mark the lease ReleaseFailed.</summary>
    private async Task ReleaseRootsAsync(CancellationToken deadline)
    {
        try
        {
            await Task.Run(() => _roots!.Dispose(), CancellationToken.None).WaitAsync(deadline)
                .ConfigureAwait(false);
            return;
        }
        catch (Exception) when (!deadline.IsCancellationRequested)
        {
        }
        await Task.Delay(ProjectAuthorizationMonitor.RootReleaseRetryDelay, _time, deadline)
            .ConfigureAwait(false);
        await Task.Run(() => _roots!.Dispose(), CancellationToken.None).WaitAsync(deadline)
            .ConfigureAwait(false);
    }

    private void MarkReleaseFailed()
    {
        lock (_sync) _releaseFailed = true;
        _monitor.NotifyReleaseFailed(this);
    }
}
