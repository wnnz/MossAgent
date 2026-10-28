using System.Security.Principal;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// Process-wide reference-counted ledger of AppContainer DACL grants keyed by
/// (sid, path). Released grants stay for a grace period so follow-up launches reuse
/// them; a background sweep reclaims grants idle longer than the timeout.
/// </summary>
public sealed class WindowsSandboxGrantLedger : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SweepPeriod = TimeSpan.FromMinutes(2);
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Sid, string Path), WindowsSandboxGrantEntry> _entries = new();
    private readonly Dictionary<string, int> _profileUses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _profileIdle = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopped = new();
    private readonly Task _sweeper;
    private bool _disposed;

    internal WindowsSandboxGrantLedger() => _sweeper = Task.Run(SweepLoopAsync);

    public IAsyncDisposable Acquire(string sidString, SecurityIdentifier identity,
        IReadOnlyList<(string Path, bool Writable)> grants, string profileName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var acquired = new List<WindowsSandboxGrantEntry>(grants.Count);
        lock (_gate)
        {
            _profileUses[profileName] = _profileUses.TryGetValue(profileName, out var uses) ? uses + 1 : 1;
            _profileIdle.Remove(profileName);
        }
        try
        {
            foreach (var (path, writable) in grants)
            {
                var key = (Sid: sidString, Path: Path.GetFullPath(path));
                WindowsSandboxGrantEntry entry;
                lock (_gate)
                {
                    if (!_entries.TryGetValue(key, out entry!))
                        _entries[key] = entry = new WindowsSandboxGrantEntry(sidString, key.Path, writable, identity);
                }
                entry.Acquire();
                acquired.Add(entry);
            }
            return new WindowsSandboxGrantRelease(this, acquired, profileName);
        }
        catch
        {
            foreach (var entry in acquired)
                entry.Release(immediate: true);
            ReleaseSid(profileName);
            throw;
        }
    }

    // Deleting a profile while a same-key launch is between Ensure and CreateProcess
    // can fail the in-flight spawn, so released profiles linger for the same idle
    // grace as grants and are reclaimed by the sweep or Dispose.
    internal void ReleaseSid(string profileName)
    {
        lock (_gate)
        {
            if (_profileUses.TryGetValue(profileName, out var uses) && uses - 1 <= 0)
            {
                _profileUses.Remove(profileName);
                _profileIdle[profileName] = DateTimeOffset.UtcNow;
            }
            else if (uses > 0)
            {
                _profileUses[profileName] = uses - 1;
            }
        }
    }

    internal int SweepNow(TimeSpan idleThreshold)
    {
        WindowsSandboxGrantEntry[] candidates;
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
            candidates = _entries.Values
                .Where(entry => entry.IdleSince != DateTimeOffset.MaxValue || entry.IsRemoved).ToArray();
        var swept = 0;
        foreach (var entry in candidates)
        {
            if (!entry.SweepIdle(now, idleThreshold)) continue;
            lock (_gate)
            {
                // A racing Acquire may have re-referenced the entry after it swept.
                if (entry.References == 0 && entry.IsRemoved)
                    _entries.Remove((entry.Sid, entry.Path));
            }
            swept++;
        }
        string[] idleProfiles;
        lock (_gate)
            idleProfiles = _profileIdle.Where(pair => now - pair.Value >= idleThreshold)
                .Select(pair => pair.Key).ToArray();
        foreach (var profile in idleProfiles)
        {
            WindowsAppContainerIdentity.DeleteProfile(profile);
            lock (_gate)
            {
                // Only reclaim a profile that stayed idle; a racing Acquire clears the stamp.
                if (_profileIdle.TryGetValue(profile, out var idleSince) && now - idleSince >= idleThreshold)
                    _profileIdle.Remove(profile);
            }
        }
        return swept;
    }

    private async Task SweepLoopAsync()
    {
        using var timer = new PeriodicTimer(SweepPeriod);
        try
        {
            while (await timer.WaitForNextTickAsync(_stopped.Token).ConfigureAwait(false))
                SweepNow(IdleTimeout);
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopped.Cancel();
        try { _sweeper.Wait(TimeSpan.FromSeconds(10)); }
        catch (AggregateException) { }
        WindowsSandboxGrantEntry[] remaining;
        string[] profiles;
        lock (_gate)
        {
            remaining = _entries.Values.ToArray();
            _entries.Clear();
            profiles = _profileUses.Keys.Concat(_profileIdle.Keys).ToArray();
            _profileUses.Clear();
            _profileIdle.Clear();
        }
        foreach (var entry in remaining)
            entry.RemoveBestEffort();
        foreach (var profile in profiles)
            WindowsAppContainerIdentity.DeleteProfile(profile);
        _stopped.Dispose();
    }
}
