using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalWriteTool(ITerminalSessionManager terminals, IAuthorizedPathResolver paths) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.write", "向当前任务的交互式终端发送输入。",
        """{"type":"object","required":["input"],"properties":{"input":{"type":"string","maxLength":32768},"sessionId":{"type":"string","maxLength":36}}}""",
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
        var input = request.Arguments.GetRequiredString("input");
        cancellationToken.ThrowIfCancellationRequested();
        var workingDirectory = paths.Resolve(context.WorkingDirectory, context);
        var session = TerminalRequestedSession.Read(request);
        if (terminals is ITerminalSessionControl control)
            await control.WriteAsync(context.TaskId, session, workingDirectory, input, cancellationToken).ConfigureAwait(false);
        else if (session is not null) return ToolResult.Failure("终端不支持会话身份校验。", "terminal_session_unavailable");
        else await terminals.WriteAsync(context.TaskId, input, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ToolResult.Success($"已向终端写入 {input.Length} 个字符。");
    }
}
