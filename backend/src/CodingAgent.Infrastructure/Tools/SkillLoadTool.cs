using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Tools;

public class SkillLoadTool(ISkillRepository skills) : ITool
{
    public string Name => "skill_load";
    public string Description => "按名加载技能正文。参数: {\"name\": \"技能名\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        var name = ToolArguments.GetString(argumentsJson, "name");
        if (name.Length == 0)
        {
            return ToolResult.Fail("name 不能为空");
        }
        var skill = await skills.GetByNameAsync(name, ct);
        if (skill is null || !skill.Enabled)
        {
            return ToolResult.Fail($"技能不存在或未启用: {name}");
        }
        return ToolResult.Ok($"# {skill.Name}\n\n{skill.Instructions}");
    }
}
