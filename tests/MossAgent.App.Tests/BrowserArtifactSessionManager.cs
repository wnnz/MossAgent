using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class BrowserArtifactSessionManager(BrowserArtifactTestSession session) : ITaskBrowserSessionManager
{
    public Guid? Owner { get; private set; }
    public IBrowserSession GetLazySession(Guid taskId)
    {
        if (Owner is not null && Owner != taskId) throw new InvalidOperationException("Fixture session belongs to another task.");
        Owner = taskId;
        return session;
    }
    public async Task ReleaseAsync(Guid taskId) { if (Owner == taskId) await session.DisposeAsync(); }
    public ValueTask DisposeAsync() => session.DisposeAsync();
}
