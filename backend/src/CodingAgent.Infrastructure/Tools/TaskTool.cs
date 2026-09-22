using System.Text.Json;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Tools;

/// <summary>task 工具：触发子代理执行，返回最终文本。通过 ISubAgentRunner 解耦（防止循环依赖）。</summary>
public class TaskTool(ISubAgentRepository subAgents, ISubAgentRunner runner) : ITool
{
    public string Name => "task";
    public string Description => "调用子代理完成独立任务。参数: {\"subagent\": \"子代理名称\", \"input\": \"任务输入\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"subagent":{"type":"string","description":"子代理名称"},"input":{"type":"string","description":"任务输入"}},"required":["subagent","input"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        var name = ToolArguments.GetString(argumentsJson, "subagent");
        var input = ToolArguments.GetString(argumentsJson, "input");
        if (name.Length == 0 || input.Length == 0)
        {
            return ToolResult.Fail("subagent 和 input 不能为空");
        }

        var subAgent = (await subAgents.GetAllAsync(enabledOnly: true, ct))
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (subAgent is null)
        {
            return ToolResult.Fail($"子代理不存在或未启用: {name}");
        }

        var result = await runner.RunAsync(subAgent, input, ct);
        return ToolResult.Ok(result);
    }
}
