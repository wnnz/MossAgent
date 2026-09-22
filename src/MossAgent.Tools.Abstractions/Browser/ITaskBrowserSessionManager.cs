namespace MossAgent.Tools.Abstractions.Browser;

public interface ITaskBrowserSessionManager : IAsyncDisposable
{
    IBrowserSession GetLazySession(Guid taskId);
    Task ReleaseAsync(Guid taskId);
}

