namespace MossAgent.Tools.Abstractions.Browser;

public sealed class BrowserTakeoverException(string message, BrowserControlState state) : Exception(message)
{
    public const string TakeoverActiveErrorCode = "browser_takeover_active";
    public const string SessionDisconnectedErrorCode = "browser_session_disconnected";

    public BrowserControlState State { get; } = state;

    public string ErrorCode => State == BrowserControlState.Disconnected
        ? SessionDisconnectedErrorCode
        : TakeoverActiveErrorCode;
}
