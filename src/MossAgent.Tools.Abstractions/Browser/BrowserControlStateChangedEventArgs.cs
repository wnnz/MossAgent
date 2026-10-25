namespace MossAgent.Tools.Abstractions.Browser;

public sealed class BrowserControlStateChangedEventArgs(Guid taskId, BrowserControlState state) : EventArgs
{
    public Guid TaskId { get; } = taskId;

    public BrowserControlState State { get; } = state;
}
