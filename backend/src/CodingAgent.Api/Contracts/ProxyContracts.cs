namespace CodingAgent.Api.Contracts.Proxies;

public record ProxyDto(int Id, string Name, string Scheme, string Host, int Port, string? Username, string? Password, bool Enabled);
public record CreateProxyRequest(string Name, string Scheme, string Host, int Port, string? Username, string? Password, bool Enabled);
public record UpdateProxyRequest(string Name, string Scheme, string Host, int Port, string? Username, string? Password, bool Enabled);
public record ProxyTestResponse(bool Success, string Message, int? LatencyMs);
