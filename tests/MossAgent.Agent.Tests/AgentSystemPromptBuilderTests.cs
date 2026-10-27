using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class AgentSystemPromptBuilderTests
{
    [Fact]
    public void Build_DescribesTaskBoundaryWithoutSecrets()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "moss-agent-root"));
        var additional = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "moss-agent-shared"));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), root, [root, additional], ApprovalPolicy.AskEveryTime);

        var prompt = AgentSystemPromptBuilder.Build(context);

        Assert.Contains("AskEveryTime", prompt, StringComparison.Ordinal);
        Assert.Contains(root, prompt, StringComparison.Ordinal);
        Assert.Contains(additional, prompt, StringComparison.Ordinal);
        Assert.Contains("不得尝试访问", prompt, StringComparison.Ordinal);
        Assert.Contains("绝不输出", prompt, StringComparison.Ordinal);
        Assert.Equal(1, Count(prompt, $"- {root}"));
    }

    [Fact]
    public void Build_DoesNotAdvertiseUnlistedWorkingDirectoryAsAuthorized()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var authorized = Path.Combine(basePath, "authorized");
        var outside = Path.Combine(basePath, "outside");
        var context = new ToolExecutionContext(
            Guid.NewGuid(), outside, [authorized], ApprovalPolicy.FullAccess);

        var prompt = AgentSystemPromptBuilder.Build(context);

        Assert.Contains($"当前工作目录：{outside}", prompt, StringComparison.Ordinal);
        Assert.Contains($"- {authorized}", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain($"- {outside}", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AppendsLabeledSkillSectionBeforeGuardrails()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "moss-agent-root"));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

        var prompt = AgentSystemPromptBuilder.Build(context, "agent rules", "### demo v1.0 — skills/a/SKILL.md\nSKILL BODY");

        Assert.Contains("以下为已启用本地插件提供的技能指令（来自任务激活快照）：", prompt, StringComparison.Ordinal);
        Assert.Contains("### demo v1.0 — skills/a/SKILL.md\nSKILL BODY", prompt, StringComparison.Ordinal);
        var section = prompt.IndexOf("技能指令", StringComparison.Ordinal);
        var firstGuardrail = prompt.IndexOf("先检查现有代码和状态", StringComparison.Ordinal);
        var secretsGuardrail = prompt.IndexOf("绝不输出", StringComparison.Ordinal);
        Assert.True(section > -1 && firstGuardrail > -1 && section < firstGuardrail && section < secretsGuardrail);
        var agentBlock = prompt.IndexOf("agent rules", StringComparison.Ordinal);
        Assert.True(agentBlock > -1 && agentBlock < section);
    }

    [Fact]
    public void Build_ToolsDisabledStillIncludesTheSkillSection()
    {
        var context = new ToolExecutionContext(
            Guid.NewGuid(), string.Empty, [], ApprovalPolicy.ReadOnly) { ToolsEnabled = false };

        var prompt = AgentSystemPromptBuilder.Build(context, null, "SKILL BODY");

        Assert.Contains("以下为已启用本地插件提供的技能指令（来自任务激活快照）：", prompt, StringComparison.Ordinal);
        Assert.Contains("SKILL BODY", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_TruncatesSkillSectionAtTwentyFourThousandCharacters()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "moss-agent-root"));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);
        var skills = new string('s', 24_000) + "OVERFLOW MARKER";

        var prompt = AgentSystemPromptBuilder.Build(context, null, skills);

        Assert.DoesNotContain("OVERFLOW MARKER", prompt, StringComparison.Ordinal);
        Assert.Contains(new string('s', 24_000), prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithoutSkillsLeavesThePromptUnchanged()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "moss-agent-root"));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

        Assert.Equal(AgentSystemPromptBuilder.Build(context, "agent rules"),
            AgentSystemPromptBuilder.Build(context, "agent rules", null));
        Assert.DoesNotContain("技能指令",
            AgentSystemPromptBuilder.Build(context, "agent rules", string.Empty), StringComparison.Ordinal);
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;
}
