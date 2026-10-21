using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalStartTool(
    ITerminalSessionManager terminals,
    IAuthorizedPathResolver paths) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.start", "为当前任务启动交互式 PowerShell ConPTY 终端。",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(context.WorkingDirectory, context);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workingDirectory = paths.Resolve(context.WorkingDirectory, context);
        if (context.WorkspaceGuard is ITaskWorkspaceLeaseProvider)
        {
            if (terminals is not IAuthorizedTerminalSessionManager authorized)
                throw new MossAgent.Domain.WorkspaceAuthorizationException();
            await authorized.StartAuthorizedAsync(context, cancellationToken);
        }
        else await terminals.StartAsync(context.TaskId, workingDirectory, cancellationToken);
        return ToolResult.Success("交互式终端已启动。");
    }
}
