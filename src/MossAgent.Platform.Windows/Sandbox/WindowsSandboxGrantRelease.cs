namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// Released when the sandboxed process is disposed. Grants become idle rather than
/// being removed immediately so a quick follow-up launch can reuse them; the ledger
/// sweeper reclaims them after the idle grace period.
/// </summary>
internal sealed class WindowsSandboxGrantRelease(
    WindowsSandboxGrantLedger ledger, IReadOnlyList<WindowsSandboxGrantEntry> entries,
    string profileName) : IAsyncDisposable
{
    private int _released;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return ValueTask.CompletedTask;
        foreach (var entry in entries)
            entry.Release(immediate: false);
        ledger.ReleaseSid(profileName);
        return ValueTask.CompletedTask;
    }
}
