namespace MossAgent.Domain;

public sealed record AiProvider(
    Guid Id,
    string Name,
    ProviderProtocol Protocol,
    Uri BaseUri,
    string ApiKey,
    Guid? ProxyId,
    bool IsDefault,
    bool IsEnabled,
    IReadOnlyDictionary<string, string> Headers);

