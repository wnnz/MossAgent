using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserDevToolsTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.devtools", BrowserToolText.Get("DeveloperToolsDescription"),
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("The task has no attached browser session.");
        try
        {
            await session.OpenDeveloperToolsAsync(cancellationToken);
            return ToolResult.Success(BrowserToolText.Get("DeveloperToolsOpened"));
        }
        catch (NotSupportedException)
        {
            return ToolResult.Failure(BrowserToolText.Get("DeveloperToolsUnsupported"), "browser_devtools_unsupported");
        }
    }
}
