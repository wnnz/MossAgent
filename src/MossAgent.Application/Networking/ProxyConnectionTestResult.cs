namespace MossAgent.Application.Networking;

public sealed record ProxyConnectionTestResult(
    bool IsSuccess,
    TimeSpan Elapsed,
    string Message);
