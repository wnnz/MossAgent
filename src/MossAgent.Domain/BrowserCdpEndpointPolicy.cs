namespace MossAgent.Domain;

public static class BrowserCdpEndpointPolicy
{
    public static bool IsAllowed(Uri? endpoint) =>
        endpoint is { IsAbsoluteUri: true, IsLoopback: true }
        && !string.IsNullOrWhiteSpace(endpoint.Host)
        && string.IsNullOrEmpty(endpoint.UserInfo)
        && endpoint.Scheme is "http" or "https" or "ws" or "wss";
}
