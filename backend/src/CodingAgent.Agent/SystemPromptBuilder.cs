using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Mcp;
using CodingAgent.Infrastructure.Tools;
using CodingAgent.Infrastructure.Database;

namespace CodingAgent.Agent;

/// <summary>构建系统提示词：人设 + 工作区摘要 + 记忆 + 技能索引 + MCP 工具 + 子代理调用规则。</summary>
public class SystemPromptBuilder(
    IMemoryRepository memories,
    ISkillRepository skills,
    ISubAgentRepository subAgents,
    ToolRegistry toolRegistry)
{
    public async Task<string> BuildAsync(string workspacePath, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("""
            你是一个严谨、务实的 Coding Agent（编程助手），运行在用户的本地工作区中。
            核心原则：最小必要改动、保持安全与向后兼容、先读后写、修复根因。
            你可以通过内置工具读写文件、执行命令、检索记忆与技能；遇到需要独立上下文的子任务时使用 task 工具调用子代理。
            修改代码后应主动验证（运行测试或构建）。使用中文与用户沟通。
            """);

        sb.AppendLine();
        sb.AppendLine($"## 工作区\n\n当前工作区目录: `{workspacePath}`\n所有文件操作与命令执行都被限制在该目录内。");

        var memoryItems = await memories.GetAllAsync(ct: ct);
        if (memoryItems.Count > 0)
        {
            sb.AppendLine("\n## 记忆摘要\n（可用 memory_search 按关键词检索完整内容，memory_save 保存新记忆）");
            foreach (var m in memoryItems.Take(30))
            {
                var summary = m.Content.Length > 200 ? m.Content[..200] + "…" : m.Content;
                sb.AppendLine($"- [{m.Scope.ToString().ToLowerInvariant()}] **{m.Title}**: {summary.ReplaceLineEndings(" ")}");
            }
        }

        var skillItems = await skills.GetAllAsync(enabledOnly: true, ct);
        if (skillItems.Count > 0)
        {
            sb.AppendLine("\n## 技能索引\n（需要时用 skill_load 按名加载完整正文）");
            foreach (var s in skillItems)
            {
                sb.AppendLine($"- **{s.Name}**: {s.Description}");
            }
        }

        var tools = await toolRegistry.GetToolsAsync(ct);
        var mcpTools = tools.Where(t => t.Name.StartsWith("mcp__", StringComparison.Ordinal)).ToList();
        if (mcpTools.Count > 0)
        {
            sb.AppendLine("\n## MCP 工具（已连接）\n（直接按工具名调用）");
            foreach (var t in mcpTools)
            {
                var desc = t.Description.Length > 150 ? t.Description[..150] + "…" : t.Description;
                sb.AppendLine($"- `{t.Name}`: {desc.ReplaceLineEndings(" ")}");
            }
        }

        var agents = await subAgents.GetAllAsync(enabledOnly: true, ct);
        if (agents.Count > 0)
        {
            sb.AppendLine("\n## 子代理及调用规则\n（通过 task 工具调用，参数: {\"subagent\": 名称, \"input\": 任务输入}；按规则决策何时调用）");
            foreach (var a in agents)
            {
                sb.AppendLine($"- **{a.Name}**: {a.Description}");
                if (!string.IsNullOrWhiteSpace(a.InvocationRule))
                {
                    sb.AppendLine($"  何时调用: {a.InvocationRule}");
                }
            }
        }

        return sb.ToString();
    }
}
