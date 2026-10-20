using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class PinnedModelContextTests
{
    [Fact]
    public void LongHistoryRetainsCompleteUserSnapshotAndLatestPromptWithinBudget()
    {
        var snapshot = "UNTRUSTED-PR-SNAPSHOT" + new string('p', 512);
        var latest = new ModelMessage(ModelRole.User, "current review question");
        ModelMessage[] messages = [new(ModelRole.System, "fixed instructions"),
            new(ModelRole.User, "discarded oldest question"), new(ModelRole.Assistant, "discarded oldest answer"),
            new(ModelRole.User, new string('h', 40000)), new(ModelRole.Assistant, new string('a', 40000)), latest];
        var model = Model(4096);

        var result = ModelContextWindowBuilder.Build(messages, [], model, snapshot);

        Assert.Equal(ModelRole.System, result[0].Role);
        Assert.Equal(ModelRole.User, result[1].Role);
        Assert.Equal(snapshot, result[1].Content);
        Assert.Single(result, message => message.Content == snapshot);
        Assert.Same(latest, result[^1]);
        Assert.DoesNotContain(result, message => message.Content.Contains("discarded oldest", StringComparison.Ordinal));
        Assert.InRange(ModelContextWindowBuilder.Estimate(result), 1, Budget(model));
        Assert.DoesNotContain(messages, message => message.Content == snapshot);
    }

    [Fact]
    public void ToolDescriptorsAreChargedAlongsideTheCompleteSnapshot()
    {
        var snapshot = new string('p', 512);
        var model = Model(4096);
        ToolDescriptor[] tools = [new("read", new string('d', 4096), "{}", ToolRiskLevel.ReadOnly, ToolCapability.FileRead)];
        ModelMessage[] messages = [new(ModelRole.System, "instructions"), new(ModelRole.User, new string('q', 40000))];

        var result = ModelContextWindowBuilder.Build(messages, tools, model, snapshot);

        Assert.Equal(snapshot, result[1].Content);
        var toolCost = tools.Sum(tool => tool.Name.Length + tool.Description.Length + tool.InputSchemaJson.Length + 64);
        Assert.InRange(ModelContextWindowBuilder.Estimate(result), 1, Budget(model) - toolCost);
    }

    [Fact]
    public void OversizedSnapshotFailsRatherThanClippingOrPromotingUntrustedData()
    {
        var snapshot = "untrusted private snapshot" + new string('p', 24576);

        var error = Assert.Throws<PinnedModelContextException>(() => ModelContextWindowBuilder.Build(
            [new(ModelRole.System, "instructions"), new(ModelRole.User, "latest")], [], Model(1024), snapshot));

        Assert.DoesNotContain(snapshot, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1248, true)]
    [InlineData(1249, false)]
    public void SnapshotAlwaysReservesTheLatestTurnMinimum(int snapshotLength, bool fits)
    {
        var snapshot = new string('p', snapshotLength);
        ModelMessage[] messages = [new(ModelRole.User, new string('q', 4000))];
        var model = Model(1024);
        if (!fits)
        {
            Assert.Throws<PinnedModelContextException>(() => ModelContextWindowBuilder.Build(messages, [], model, snapshot));
            return;
        }

        var result = ModelContextWindowBuilder.Build(messages, [], model, snapshot);

        Assert.Equal(snapshot, result[0].Content);
        Assert.NotEmpty(result[1].Content);
        Assert.InRange(ModelContextWindowBuilder.Estimate(result), 1, Budget(model));
    }

    [Fact]
    public void LatestToolMetadataCannotDisplaceTheSnapshotOrExceedTheBudget()
    {
        var call = new ModelToolCall("call", "read", JsonSerializer.SerializeToElement(new { value = new string('x', 4000) }));
        ModelMessage[] messages = [new(ModelRole.User, "question"), new(ModelRole.Assistant, "", ToolCalls: [call])];

        Assert.Throws<PinnedModelContextException>(() => ModelContextWindowBuilder.Build(messages, [], Model(1024), "snapshot"));
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(10000)]
    public void SnapshotDoesNotMaterializeDiscardedHistoryOrAccumulateAcrossRequests(int oldTurnCount)
    {
        var oldContent = new string('x', 500);
        var messages = Enumerable.Range(0, oldTurnCount).SelectMany(_ => new ModelMessage[]
            { new(ModelRole.User, oldContent), new(ModelRole.Assistant, oldContent) })
            .Append(new(ModelRole.User, "latest")).ToArray();
        var model = Model(800);
        var snapshot = new string('p', 100);
        ModelContextWindowBuilder.Build(messages, [], model, snapshot);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var result = ModelContextWindowBuilder.Build(messages, [], model, snapshot);

        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 1, 32000);
        Assert.Single(result, message => message.Content == snapshot);
        Assert.Equal("latest", result[^1].Content);
        Assert.InRange(ModelContextWindowBuilder.Estimate(result), 1, 1024);
    }

    [Fact]
    public void NullSnapshotRetainsTheExistingMessageIdentityAndOrdering()
    {
        ModelMessage[] messages = [new(ModelRole.System, "instructions"), new(ModelRole.User, "question")];
        var model = Model(4096);

        var original = ModelContextWindowBuilder.Build(messages, [], model);
        var explicitNull = ModelContextWindowBuilder.Build(messages, [], model, null);

        Assert.Equal(original, explicitNull);
        for (var index = 0; index < original.Count; index++) Assert.Same(original[index], explicitNull[index]);
    }

    private static int Budget(ModelProfile model) => (model.ContextLength - model.MaximumOutputTokens - 512) * 4;

    private static ModelProfile Model(int contextLength) => new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "Fixture",
        contextLength, 128, "off", null, null, false, false, true, true, "{}");
}
