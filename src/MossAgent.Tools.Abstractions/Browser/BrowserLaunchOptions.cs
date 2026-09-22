namespace MossAgent.Tools.Abstractions.Browser;

public sealed record BrowserLaunchOptions(
    BrowserEngine Engine,
    BrowserProfileMode ProfileMode,
    string? ProfileDirectory,
    Uri? CdpEndpoint,
    Uri? ProxyUri,
    string? ProxyUsername,
    string? ProxyPassword);

