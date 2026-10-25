using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class StubBrowserTakeoverController : IBrowserTakeoverController
{
    private readonly Dictionary<Guid, BrowserControlState> _states = [];
    public List<(Guid TaskId, string Action)> Calls { get; } = [];
    public Func<Guid, CancellationToken, Task<BrowserControlState>>? ReturnBehavior { get; set; }

    public event EventHandler<BrowserControlStateChangedEventArgs>? StateChanged;

    public bool HasControlState(Guid taskId) => _states.ContainsKey(taskId);

    public BrowserControlState GetControlState(Guid taskId) =>
        _states.GetValueOrDefault(taskId);

    public void SetState(Guid taskId, BrowserControlState state)
    {
        _states[taskId] = state;
        StateChanged?.Invoke(this, new BrowserControlStateChangedEventArgs(taskId, state));
    }

    public void RemoveState(Guid taskId)
    {
        _states.Remove(taskId);
        StateChanged?.Invoke(
            this, new BrowserControlStateChangedEventArgs(taskId, BrowserControlState.Automated));
    }

    public Task<BrowserControlState> BeginUserTakeoverAsync(
        Guid taskId, CancellationToken cancellationToken)
    {
        Calls.Add((taskId, "takeover"));
        SetState(taskId, BrowserControlState.HandedToUser);
        return Task.FromResult(BrowserControlState.HandedToUser);
    }

    public Task<BrowserControlState> ReturnControlAsync(
        Guid taskId, CancellationToken cancellationToken)
    {
        Calls.Add((taskId, "return"));
        if (ReturnBehavior is not null)
        {
            return ReturnBehavior(taskId, cancellationToken);
        }

        SetState(taskId, BrowserControlState.Automated);
        return Task.FromResult(BrowserControlState.Automated);
    }

    public BrowserControlState MarkDisconnected(Guid taskId)
    {
        Calls.Add((taskId, "disconnect"));
        SetState(taskId, BrowserControlState.Disconnected);
        return BrowserControlState.Disconnected;
    }
}
