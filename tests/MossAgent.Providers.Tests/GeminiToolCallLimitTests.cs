using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiToolCallLimitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactCallLimit_IsAcceptedBeforeAnExtraCallRejectsTheWholeBatch(bool singleFrame)
    {
        var parser = Parser();
        if (singleFrame)
            Assert.Empty(parser.Parse(GeminiStreamTestData.Data(Enumerable.Repeat(GeminiStreamTestData.Call(), 128).ToArray())));
        else
            for (var index = 0; index < 128; index++) Assert.Empty(parser.Parse(GeminiStreamTestData.Data([GeminiStreamTestData.Call()])));

        GeminiStreamBoundaryTests.AssertFailure(parser.Parse(GeminiStreamTestData.Data([GeminiStreamTestData.Call()], "STOP")), "tool_calls_limit");
        Assert.Empty(parser.Complete());
    }

    [Fact]
    public void OneFrameOverCallLimit_IsRejectedWithoutPartialPublication()
    {
        var parser = Parser();
        var data = GeminiStreamTestData.Data(Enumerable.Repeat(GeminiStreamTestData.Call(), 129).ToArray(), "STOP");

        GeminiStreamBoundaryTests.AssertFailure(parser.Parse(data), "tool_calls_limit");
        Assert.Empty(parser.Complete());
    }

    [Fact]
    public void ExactBatchLimits_DrainOnceAfterVerifiedCompletion()
    {
        var parser = Parser();
        var parts = Enumerable.Repeat(GeminiStreamTestData.Call(), 128).ToArray();
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data(parts, "STOP")));

        var events = parser.Complete();

        var calls = events.OfType<ToolCallCompleted>().ToArray();
        Assert.Equal(128, calls.Length);
        Assert.Equal(128, calls.Select(call => call.CallId).Distinct().Count());
        Assert.Single(events.OfType<ModelCompleted>());
        Assert.Empty(parser.Complete());
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([GeminiStreamTestData.Call()], "STOP")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SingleArgumentBudget_IsInclusiveAndRejectsOverflowBeforeCloning(int excess)
    {
        var parser = Parser();
        var arguments = ArgumentAtLimit(excess);
        Assert.Equal(GeminiToolCallBuffer.MaximumArgumentCharacters + excess, JsonSerializer.Serialize(arguments).Length);

        var events = parser.Parse(GeminiStreamTestData.Data([GeminiStreamTestData.Call(arguments)], "STOP"));

        if (excess > 0)
        {
            GeminiStreamBoundaryTests.AssertFailure(events, "tool_arguments_limit");
            Assert.Empty(parser.Complete());
        }
        else
        {
            Assert.Empty(events);
            Assert.Single(parser.Complete().OfType<ToolCallCompleted>());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TotalArgumentBudget_IsSharedAcrossFrames(bool exceed)
    {
        var parser = Parser();
        var call = GeminiStreamTestData.Call(ArgumentAtLimit(0));
        for (var index = 0; index < 4; index++) Assert.Empty(parser.Parse(GeminiStreamTestData.Data([call])));
        var last = parser.Parse(GeminiStreamTestData.Data(exceed ? [GeminiStreamTestData.Call()] : [], "STOP"));

        if (exceed)
        {
            GeminiStreamBoundaryTests.AssertFailure(last, "tool_arguments_limit");
            Assert.Empty(parser.Complete());
        }
        else
        {
            Assert.Empty(last);
            Assert.Equal(4, parser.Complete().OfType<ToolCallCompleted>().Count());
        }
    }

    [Theory]
    [InlineData(512)]
    [InlineData(513)]
    public void ToolIdentityBudget_IsCheckedWithoutEchoingItsContent(int length)
    {
        var parser = Parser();
        var data = GeminiStreamTestData.Data([new { functionCall = new { name = new string('x', length), args = new { } } }], "STOP");

        var events = parser.Parse(data);

        if (length > 512) GeminiStreamBoundaryTests.AssertFailure(events, "invalid_tool_arguments");
        else Assert.Single(parser.Complete().OfType<ToolCallCompleted>());
    }

    private static GeminiStreamParser Parser() => new(new ProviderToolNameMap([GeminiStreamTestData.Tool]));

    private static object ArgumentAtLimit(int excess)
    {
        var overhead = JsonSerializer.Serialize(new { payload = string.Empty }).Length;
        return new { payload = new string('x', GeminiToolCallBuffer.MaximumArgumentCharacters - overhead + excess) };
    }
}
