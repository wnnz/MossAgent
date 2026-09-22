namespace MossAgent.Domain;

public sealed record ProxyProfile(
    Guid Id,
    string Name,
    ProxyProtocol Protocol,
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool IsDefault,
    bool IsEnabled)
{
    public Uri ToUri() => new($"{Protocol.ToString().ToLowerInvariant()}://{Host}:{Port}");
}

