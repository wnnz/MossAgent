using MossAgent.Application.Models;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiParserProjectionTests
{
    [Fact]
    public void TextFrames_RetainIndependentSnapshotsThroughCompletionAndFailure()
    {
        var parser = Parser();
        var first = parser.Parse(GeminiStreamTestData.Data([
            new { text = "first" }, new { text = "hidden", thought = true },
            new { text = string.Empty }, new { text = "second" }]));
        var firstEvents = first.ToArray();

        Assert.Equal(["first", "second"], first.Select(item => Assert.IsType<TextDelta>(item).Text));
        var next = parser.Parse(GeminiStreamTestData.Data([new { text = "third" }], "STOP"));
        Assert.Equal("third", Assert.IsType<TextDelta>(Assert.Single(next)).Text);
        Assert.Single(parser.Complete().OfType<ModelCompleted>());
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([new { text = "ignored" }])));
        Assert.Equal(firstEvents, first);
        for (var index = 0; index < first.Count; index++) Assert.Same(firstEvents[index], first[index]);

        var failing = Parser();
        var retained = failing.Parse(GeminiStreamTestData.Data([new { text = "retained" }]));
        GeminiStreamBoundaryTests.AssertFailure(failing.Parse("invalid-json"), "invalid_response");
        Assert.Equal("retained", Assert.IsType<TextDelta>(Assert.Single(retained)).Text);
        Assert.Equal(firstEvents, first);
    }

    [Fact]
    public void TextProjection_DoesNotBypassSignedContinuationCapture()
    {
        var request = GeminiStreamTestData.Request();
        var parser = Parser(request);
        var first = parser.Parse(GeminiStreamTestData.Data([
            new { text = "thought", thought = true, thoughtSignature = "signed-fixture" },
            new { text = "answer" }]));
        Assert.Equal("answer", Assert.IsType<TextDelta>(Assert.Single(first)).Text);
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([], "STOP")));

        var completion = Assert.IsType<ModelCompleted>(Assert.Single(parser.Complete()));

        Assert.NotNull(completion.Continuation);
        Assert.Equal(request.Provider.Id, completion.Continuation.ProviderId);
        var parts = completion.Continuation.Payload.GetProperty("parts");
        Assert.Equal("signed-fixture", parts[0].GetProperty("thoughtSignature").GetString());
        Assert.Equal("answer", parts[1].GetProperty("text").GetString());
        Assert.Equal("answer", Assert.IsType<TextDelta>(Assert.Single(first)).Text);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"candidates\":[]}")]
    public void MetadataFrames_RemainNonterminalAndIndependent(string data)
    {
        var parser = Parser();
        var metadata = parser.Parse(data);

        Assert.Empty(metadata);
        Assert.False(parser.IsTerminal);
        Assert.Equal("answer", Assert.IsType<TextDelta>(Assert.Single(
            parser.Parse(GeminiStreamTestData.Data([new { text = "answer" }], "STOP")))).Text);
        Assert.Single(parser.Complete());
        Assert.Empty(metadata);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(129)]
    public void InterleavedTextAndThoughts_DoNotConsumeTheToolCountBudget(int count)
    {
        var parser = Parser();
        var parts = new List<object>();
        for (var index = 0; index < count; index++)
        {
            parts.Add(new { text = "answer-" + index });
            parts.Add(new { text = "hidden", thought = true });
            parts.Add(GeminiStreamTestData.Call(new { index }));
        }

        var result = parser.Parse(GeminiStreamTestData.Data(parts.ToArray(), "STOP"));

        if (count > GeminiToolCallBuffer.MaximumCalls)
        {
            GeminiStreamBoundaryTests.AssertFailure(result, "tool_calls_limit");
            Assert.Empty(parser.Complete());
            return;
        }
        Assert.Equal(Enumerable.Range(0, count).Select(index => "answer-" + index),
            result.Select(item => Assert.IsType<TextDelta>(item).Text));
        var calls = parser.Complete().OfType<ToolCallCompleted>().ToArray();
        Assert.Equal(Enumerable.Range(0, count), calls.Select(call => call.Arguments.GetProperty("index").GetInt32()));
        Assert.Equal(count, result.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidLatePart_DiscardsEarlierTextAndPendingCalls(bool invalidIdentity)
    {
        var parser = Parser();
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([GeminiStreamTestData.Call()])));
        var invalid = invalidIdentity
            ? new { functionCall = new { name = GeminiStreamTestData.WireName, id = " ", args = new { } } }
            : (object)new { functionCall = new { name = " ", args = new { } } };

        var result = parser.Parse(GeminiStreamTestData.Data([new { text = "discarded" }, invalid], "STOP"));

        GeminiStreamBoundaryTests.AssertFailure(result, invalidIdentity ? "invalid_response" : "invalid_tool_arguments");
        Assert.Empty(parser.Complete());
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([new { text = "ignored" }])));
    }

    [Theory]
    [InlineData(512)]
    [InlineData(513)]
    public void NativeIdentityLimit_IsPreservedWithoutEchoingTheIdentity(int length)
    {
        var parser = Parser();
        var identity = new string('x', length);
        var result = parser.Parse(GeminiStreamTestData.Data([
            new { functionCall = new { name = GeminiStreamTestData.WireName, id = identity } }], "STOP"));

        if (length > GeminiToolCallBuffer.MaximumIdentityCharacters)
        {
            GeminiStreamBoundaryTests.AssertFailure(result, "invalid_response");
            Assert.DoesNotContain(identity, Assert.IsType<ModelFailed>(Assert.Single(result)).Message);
            Assert.Empty(parser.Complete());
            return;
        }
        Assert.Empty(result);
        Assert.Equal(identity, Assert.Single(parser.Complete().OfType<ToolCallCompleted>()).ProviderCallId);
    }

    [Fact]
    public void EscapedNativeIdentityAndArguments_SurviveFrameDisposal()
    {
        const string identity = "工具\\调用\n\"😀";
        var parser = Parser();
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([
            new { functionCall = new { name = GeminiStreamTestData.WireName, id = identity, args = new { text = identity } } }], "STOP")));

        var call = Assert.Single(parser.Complete().OfType<ToolCallCompleted>());

        Assert.Equal(GeminiStreamTestData.Tool.Name, call.ToolName);
        Assert.Equal(identity, call.ProviderCallId);
        Assert.Equal(identity, call.Arguments.GetProperty("text").GetString());
    }

    private static GeminiStreamParser Parser(ModelRequest? request = null) =>
        new(new ProviderToolNameMap([GeminiStreamTestData.Tool]), request);
}
