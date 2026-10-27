namespace MossAgent.Domain.Plugins;

/// <summary>A server entry declared by a portable root <c>mcp.json</c>. The transport is
/// recorded verbatim; validation and launch are separate concerns outside preview.</summary>
public sealed record PluginMcpServerDeclaration(string Name, string TransportType);
