using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ModelContextWindowBuilderTests
{
    [Fact]
    public void Build_DropsOldestTurnsAndPreservesLatestTurn()
    {
        var messages = new[]
        {
            Message(ModelRole.System, new string('s', 800)),
            Message(ModelRole.User, "old-" + new string('a', 1800)),
            Message(ModelRole.Assistant, "old-answer"),
            Message(ModelRole.User, "latest-" + new string('b', 1800)),
            Message(ModelRole.Assistant, "latest-answer")
        };

        var result = ModelContextWindowBuilder.Build(messages, [], Model(contextLength: 900));

        Assert.Contains(result, static message => message.Role == ModelRole.System);
        Assert.DoesNotContain(result, static message => message.Content.StartsWith("old-"));
        Assert.Contains(result, static message => message.Content.StartsWith("latest-"));
        Assert.Contains(result, static message => message.Content == "latest-answer");
    }

    [Fact]
    public void Build_PreservesToolCallAndResultAsOneLatestGroup()
    {
        var arguments = JsonSerializer.SerializeToElement(new { path = "README.md" });
        var call = new ModelToolCall("call-1", "filesystem.read_file", arguments);
        var messages = new[]
        {
            Message(ModelRole.User, new string('o', 3000)),
            Message(ModelRole.Assistant, "old"),
            Message(ModelRole.User, "inspect"),
            new ModelMessage(ModelRole.Assistant, string.Empty, ToolCalls: [call]),
            new ModelMessage(ModelRole.Tool, new string('x', 3000), "call-1", call.Name)
        };

        var result = ModelContextWindowBuilder.Build(messages, [], Model(contextLength: 800));

        Assert.Contains(result, static message => message.ToolCalls?.Single().CallId == "call-1");
        Assert.Contains(result, static message => message.ToolCallId == "call-1");
        Assert.DoesNotContain(result, static message => message.Content.StartsWith("ooo"));
    }

    [Fact]
    public void Build_AccountsForToolDescriptorOverhead()
    {
        var messages = new[]
        {
            Message(ModelRole.User, new string('a', 1000)),
            Message(ModelRole.Assistant, new string('b', 1000)),
            Message(ModelRole.User, new string('c', 1000))
        };
        var tools = new[]
        {
            new ToolDescriptor(
                "large.tool",
                new string('d', 1200),
                "{\"type\":\"object\",\"description\":\"" + new string('e', 1200) + "\"}",
                ToolRiskLevel.ReadOnly,
                ToolCapability.FileRead)
        };

        var withTools = ModelContextWindowBuilder.Build(messages, tools, Model(contextLength: 1400));
        var withoutTools = ModelContextWindowBuilder.Build(messages, [], Model(contextLength: 1400));

        Assert.True(
            ModelContextWindowBuilder.Estimate(withTools)
            < ModelContextWindowBuilder.Estimate(withoutTools));
    }

    private static ModelMessage Message(ModelRole role, string content) => new(role, content);

    private static ModelProfile Model(int contextLength) => new(
        Guid.NewGuid(), Guid.NewGuid(), "fixture", "Fixture",
        contextLength, 128, "off", null, null,
        false, false, true, true, "{}");
}
