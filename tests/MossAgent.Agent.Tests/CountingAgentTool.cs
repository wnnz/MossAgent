using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class CountingAgentTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "test.read", "读取测试数据",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    public int Calls { get; private set; }
    public ToolRequest? LastRequest { get; private set; }
    public ToolExecutionContext? LastContext { get; private set; }

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        LastContext = context;
        return Task.FromResult(ToolResult.Success("已读取", "测试内容"));
    }
}
