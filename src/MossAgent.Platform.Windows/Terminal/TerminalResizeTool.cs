using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalResizeTool(ITerminalSessionManager terminals, IAuthorizedPathResolver paths) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.resize", "调整当前任务终端的行列尺寸。",
        """{"type":"object","required":["columns","rows"],"properties":{"columns":{"type":"integer","minimum":20,"maximum":500},"rows":{"type":"integer","minimum":5,"maximum":200},"sessionId":{"type":"string","maxLength":36}}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        paths.Resolve(context.WorkingDirectory, context);
        TerminalRequestedSession.Read(request);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workingDirectory = paths.Resolve(context.WorkingDirectory, context);
        var size = new TerminalGridSize(request.Arguments.GetOptionalInt32("columns") ?? 120, request.Arguments.GetOptionalInt32("rows") ?? 30);
        var session = TerminalRequestedSession.Read(request);
        if (terminals is ITerminalSessionControl control)
            await control.ResizeAsync(context.TaskId, session, workingDirectory, size, cancellationToken).ConfigureAwait(false);
        else if (session is not null) return ToolResult.Failure("终端不支持会话身份校验。", "terminal_session_unavailable");
        else terminals.Resize(context.TaskId, size.Columns, size.Rows);
        cancellationToken.ThrowIfCancellationRequested();
        return ToolResult.Success($"终端尺寸已调整为 {size.Columns}x{size.Rows}。");
    }
}
