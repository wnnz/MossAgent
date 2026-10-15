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

    [Fact]
    public void Build_PreservesMixedSystemOrderAndMessageIdentity()
    {
        var system = Message(ModelRole.System, "system");
        var laterSystem = Message(ModelRole.System, "later system");
        var preface = Message(ModelRole.Assistant, "preface");
        var user = Message(ModelRole.User, "question");
        var answer = Message(ModelRole.Assistant, "answer");
        var latest = Message(ModelRole.User, "latest");
        ModelMessage[] messages = [preface, system, user, laterSystem, answer, latest, answer];

        var result = ModelContextWindowBuilder.Build(messages, [], Model(4000));
        ModelMessage[] expected = [system, laterSystem, preface, user, answer, latest, answer];

        Assert.Equal(expected, result);
        for (var index = 0; index < expected.Length; index++)
            Assert.Same(expected[index], result[index]);
    }

    [Fact]
    public void Build_DoesNotSkipAnOlderTurnWithOversizedMetadata()
    {
        var call = new ModelToolCall("old-call", "test.read",
            JsonSerializer.SerializeToElement(new { value = new string('x', 2000) }));
        var latest = Message(ModelRole.User, "latest");
        ModelMessage[] messages = [Message(ModelRole.User, "small oldest"),
            Message(ModelRole.User, "large middle"),
            new(ModelRole.Assistant, "", ToolCalls: [call]), latest];

        var result = ModelContextWindowBuilder.Build(messages, [], Model(800));

        Assert.Same(latest, Assert.Single(result));
    }

    [Fact]
    public void Build_ReturnsAnIndependentSnapshotAcrossSourceChanges()
    {
        var original = Message(ModelRole.User, "original");
        var replacement = Message(ModelRole.User, "replacement");
        var messages = new List<ModelMessage> { original };
        var first = ModelContextWindowBuilder.Build(messages, [], Model(4000));
        messages.Clear();
        messages.Add(replacement);

        var second = ModelContextWindowBuilder.Build(messages, [], Model(4000));

        Assert.Same(original, Assert.Single(first));
        Assert.Same(replacement, Assert.Single(second));
    }

    [Fact]
    public void Build_HandlesEmptyAndSystemOnlyConversations()
    {
        var system = Message(ModelRole.System, "system");

        Assert.Empty(ModelContextWindowBuilder.Build([], [], Model(800)));
        Assert.Same(system, Assert.Single(ModelContextWindowBuilder.Build([system], [], Model(800))));
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(10000)]
    public void Build_AllocatesOnlyForTurnsWithinTheContextWindow(int oldTurnCount)
    {
        var oldContent = new string('x', 500);
        var messages = Enumerable.Range(0, oldTurnCount).SelectMany(_ => new ModelMessage[]
        {
            Message(ModelRole.User, oldContent), Message(ModelRole.Assistant, oldContent)
        }).Append(Message(ModelRole.User, "latest")).Append(Message(ModelRole.Assistant, "done")).ToArray();
        var model = Model(800);
        ModelContextWindowBuilder.Build(messages, [], model);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var result = ModelContextWindowBuilder.Build(messages, [], model);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 32_000);
        Assert.Equal(4, result.Count);
        Assert.Equal("latest", result[^2].Content);
        Assert.Equal("done", result[^1].Content);
        Assert.InRange(ModelContextWindowBuilder.Estimate(result), 1, 1024);
    }

    private static ModelMessage Message(ModelRole role, string content) => new(role, content);

    private static ModelProfile Model(int contextLength) => new(
        Guid.NewGuid(), Guid.NewGuid(), "fixture", "Fixture",
        contextLength, 128, "off", null, null,
        false, false, true, true, "{}");
}
