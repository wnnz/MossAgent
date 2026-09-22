namespace MossAgent.Domain;

public sealed record BrowserConfiguration(
    DefaultBrowserKind DefaultBrowser,
    BrowserProfilePreference ProfilePreference,
    string? ProfileDirectory,
    Uri? CdpEndpoint,
    Guid? ProxyId);

