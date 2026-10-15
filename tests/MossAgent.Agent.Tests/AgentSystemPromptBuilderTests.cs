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

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;
}
