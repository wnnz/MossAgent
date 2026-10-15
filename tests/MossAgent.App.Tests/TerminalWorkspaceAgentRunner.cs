using System.Runtime.CompilerServices;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class TerminalWorkspaceAgentRunner(
    Action stop, Exception exception, bool emitFinalResult) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AgentSubagentProgressEvent("stopped-call", new ToolDialogueMessage("子代理", "正在检查", true));
        stop();
        if (emitFinalResult)
            yield return new AgentToolEvent("subagent.run", "stopped-call",
                ToolResult.Failure("工具执行已取消。", "tool_cancelled"));
        await Task.Yield();
        throw exception;
    }
}
