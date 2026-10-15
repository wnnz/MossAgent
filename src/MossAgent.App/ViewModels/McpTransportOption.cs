using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record McpTransportOption(McpTransportKind Transport, string DisplayName)
{
    public static IReadOnlyList<McpTransportOption> All { get; } =
    [
        new(McpTransportKind.Stdio, "标准输入输出 (stdio)"),
        new(McpTransportKind.StreamableHttp, "Streamable HTTP")
    ];

    public static McpTransportOption FromTransport(McpTransportKind transport) =>
        All.Single(option => option.Transport == transport);
}
