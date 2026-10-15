using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiCompatibleToolLimitsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArgumentLengthLimit_AllowsExactBoundaryButRejectsOverflow(bool overflow)
    {
        var parser = CreateParser();
        var arguments = Arguments(OpenAiCompatibleToolCallAccumulator.MaximumArgumentCharacters + (overflow ? 1 : 0));
        var captured = parser.Parse(Frame(0, "call", "probe", arguments));
        if (overflow)
        {
            AssertFailure(captured, "tool_arguments_limit");
            Assert.Empty(parser.Parse("[DONE]"));
        }
        else
        {
            Assert.Empty(captured);
            var completed = parser.Parse("[DONE]");
            Assert.IsType<ToolCallCompleted>(completed[0]);
            Assert.IsType<ModelCompleted>(completed[1]);
        }
    }

    [Fact]
    public void FragmentedArguments_ApplyLimitToTheCombinedValue()
    {
        var parser = CreateParser();
        var arguments = Arguments(OpenAiCompatibleToolCallAccumulator.MaximumArgumentCharacters);
        var midpoint = arguments.Length / 2;
        Assert.Empty(parser.Parse(Frame(0, "call", "probe", arguments[..midpoint])));
        Assert.Empty(parser.Parse(Frame(0, null, null, arguments[midpoint..])));
        AssertFailure(parser.Parse(Frame(0, null, null, " ")), "tool_arguments_limit");
        Assert.Empty(parser.Complete());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TotalArgumentBudget_IsSharedAcrossCalls(bool overflow)
    {
        var parser = CreateParser();
        var arguments = Arguments(OpenAiCompatibleToolCallAccumulator.MaximumArgumentCharacters);
        for (var index = 0; index < 4; index++) Assert.Empty(parser.Parse(Frame(index, $"call-{index}", "probe", arguments)));
        if (overflow)
        {
            AssertFailure(parser.Parse(Frame(4, "extra", "probe", "{}")), "tool_arguments_limit");
            Assert.Empty(parser.Parse("[DONE]"));
        }
        else
        {
            var completed = parser.Parse("[DONE]");
            Assert.Equal(4, completed.OfType<ToolCallCompleted>().Count());
            Assert.IsType<ModelCompleted>(completed[^1]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToolCountLimit_IsBoundedAcrossSeparateFrames(bool overflow)
    {
        var parser = CreateParser();
        for (var index = 0; index < OpenAiCompatibleToolCallAccumulator.MaximumCalls; index++)
            Assert.Empty(parser.Parse(Frame(index, $"call-{index}", "probe", "{}")));
        if (overflow)
        {
            AssertFailure(parser.Parse(Frame(128, "extra", "probe", "{}")), "tool_calls_limit");
            Assert.Empty(parser.Parse("[DONE]"));
        }
        else
        {
            var completed = parser.Parse("[DONE]");
            Assert.Equal(128, completed.OfType<ToolCallCompleted>().Count());
            Assert.IsType<ModelCompleted>(completed[^1]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToolIdentityLimit_RejectsUnboundedIdsAndNames(bool identifier)
    {
        var parser = CreateParser();
        var oversized = new string('x', OpenAiCompatibleToolCallAccumulator.MaximumIdentityCharacters + 1);
        AssertFailure(parser.Parse(Frame(0, identifier ? oversized : "call", identifier ? "probe" : oversized, "{}")),
            "invalid_tool_arguments");
        Assert.Empty(parser.Parse("[DONE]"));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("tool_calls")]
    public void DataAfterChoiceFinish_CannotAddMoreToolCalls(string finish)
    {
        var parser = CreateParser();
        var terminal = JsonSerializer.Serialize(new { choices = new[] { new { index = 0, delta = new { }, finish_reason = finish } } });
        Assert.Empty(parser.Parse(terminal));
        AssertFailure(parser.Parse(Frame(0, "late-call", "probe", "{}")), "invalid_response");
        Assert.Empty(parser.Complete());
    }

    private static OpenAiCompatibleStreamParser CreateParser() => new(new ProviderToolNameMap([
        new ToolDescriptor("probe", "fixture", "{\"type\":\"object\"}", ToolRiskLevel.ReadOnly, ToolCapability.None)]));

    private static string Frame(int index, string? callId, string? name, string arguments) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { index = 0, delta = new { tool_calls = new[] { new { index, id = callId, function = new { name, arguments } } } } } }
    });

    private static string Arguments(int length) => "{\"value\":\"" + new string('x', length - 12) + "\"}";

    private static void AssertFailure(IReadOnlyList<ModelEvent> events, string code) =>
        Assert.Equal(code, Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
}
