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
    private Task<bool>? _verifyTask;
    private Task? _rootsDisposeTask;
    private bool _revoked;
    private bool _releaseFailed;
    private bool _stoppingCancelled;
    private bool _pollingDone;
    private bool _cancellingDone;
    private bool _rootsReleased;

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
            lock (_sync) _stoppingCancelled = true;
            await _polling.WaitAsync(deadline.Token).ConfigureAwait(false);
            lock (_sync) _pollingDone = true;
            try { await _cancelling.WaitAsync(deadline.Token).ConfigureAwait(false); }
            catch (AggregateException) { /* Observer failures cannot prevent lease cleanup. */ }
            lock (_sync) _cancellingDone = true;
            if (_roots is not null)
            {
                await ReleaseRootsAsync(deadline.Token).ConfigureAwait(false);
                lock (_sync) _rootsReleased = true;
            }
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
            await StartRootsDispose().WaitAsync(deadline).ConfigureAwait(false);
            return;
        }
        catch (Exception) when (!deadline.IsCancellationRequested)
        {
        }
        await Task.Delay(ProjectAuthorizationMonitor.RootReleaseRetryDelay, _time, deadline)
            .ConfigureAwait(false);
        await StartRootsDispose().WaitAsync(deadline).ConfigureAwait(false);
    }

    /// <summary>Starts a background root-session dispose and records the task so a later
    /// manual verify can observe an in-flight call instead of running Dispose concurrently.</summary>
    private Task StartRootsDispose()
    {
        lock (_sync)
        {
            _rootsDisposeTask = Task.Run(() => _roots!.Dispose(), CancellationToken.None);
            return _rootsDisposeTask;
        }
    }

    private void MarkReleaseFailed()
    {
        lock (_sync) _releaseFailed = true;
        _monitor.NotifyReleaseFailed(this);
    }

    /// <summary>Operator-triggered re-verification of a release-failed lease. Concurrent
    /// callers share one in-flight check; a true result means the lease's resources are
    /// confirmed released AND the lease was removed from the monitor. A non-failed lease
    /// verifies nothing and returns false. An unsuccessful attempt is not latched: the
    /// in-flight task is cleared so a later manual verify can retry the root release.
    /// Drain steps that timed out stay unconfirmed — their flags only record what the
    /// original drain observed, so such a lease stays CleanupUnknown.</summary>
    internal Task<bool> VerifyReleasedAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> completion;
        lock (_sync)
        {
            if (!_releaseFailed) return Task.FromResult(false);
            if (_verifyTask is not null) return _verifyTask;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _verifyTask = completion.Task;
        }
        _ = VerifyOwnedAsync(completion, cancellationToken);
        return completion.Task;
    }

    private async Task VerifyOwnedAsync(TaskCompletionSource<bool> completion,
        CancellationToken cancellationToken)
    {
        var verified = false;
        try
        {
            using var deadline = new CancellationTokenSource(ProjectAuthorizationMonitor.DrainTimeout, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
            bool stepsDone, rootsDone;
            lock (_sync)
            {
                stepsDone = _stoppingCancelled && _pollingDone && _cancellingDone
                    && _polling.IsCompleted && _cancelling.IsCompleted;
                rootsDone = _rootsReleased;
            }
            // A hung drain step leaves the resource state genuinely unknown: refuse.
            if (!stepsDone) { completion.SetResult(false); return; }
            if (!rootsDone && _roots is not null)
            {
                // The manual verify IS the retry: exactly one bounded re-dispose. A drain
                // attempt that timed out may still be running — observe that in-flight call
                // rather than invoking Dispose concurrently on the same session.
                Task? pending;
                lock (_sync) pending = _rootsDisposeTask;
                try
                {
                    if (pending is not null && !pending.IsCompleted)
                        await pending.WaitAsync(linked.Token).ConfigureAwait(false);
                    else if (pending is null || pending.IsFaulted || pending.IsCanceled)
                        await StartRootsDispose().WaitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completion.SetCanceled(cancellationToken);
                    return;
                }
                catch (Exception) { completion.SetResult(false); return; }
                lock (_sync) _rootsReleased = true;
            }
            _monitor.Release(this);
            verified = true;
            completion.SetResult(true);
        }
        catch (Exception exception) { completion.SetException(exception); }
        finally
        {
            if (!verified)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_verifyTask, completion.Task)) _verifyTask = null;
                }
            }
        }
    }
}
