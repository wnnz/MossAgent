namespace MossAgent.Tools.Abstractions.Browser;

public interface IBrowserTakeoverController
{
    bool HasControlState(Guid taskId);

    BrowserControlState GetControlState(Guid taskId);

    Task<BrowserControlState> BeginUserTakeoverAsync(Guid taskId, CancellationToken cancellationToken);

    Task<BrowserControlState> ReturnControlAsync(Guid taskId, CancellationToken cancellationToken);

    BrowserControlState MarkDisconnected(Guid taskId);

    event EventHandler<BrowserControlStateChangedEventArgs>? StateChanged;
}
