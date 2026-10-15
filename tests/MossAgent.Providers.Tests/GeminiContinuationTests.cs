using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiContinuationTests
{
    [Fact]
    public void SignedPartsAndEmptyTail_RetainOrderAndNativeCallIdsAcrossRequests()
    {
        var request = GeminiStreamTestData.Request();
        var names = new ProviderToolNameMap(request.Tools);
        var parser = new GeminiStreamParser(names, request);
        object[] parts =
        [
            new { thought = true, text = "internal", thoughtSignature = "thought+token==" },
            new { text = "visible", thoughtSignature = "text+token==" },
            SignedCall("native-one"),
            new { functionCall = new { name = GeminiStreamTestData.WireName, args = new { }, id = "native-two" } }
        ];
        var initial = parser.Parse(GeminiStreamTestData.Data(parts, "STOP"));
        Assert.Equal("visible", Assert.Single(initial.OfType<TextDelta>()).Text);
        Assert.Empty(initial.OfType<ToolCallCompleted>());
        object tail = new { text = "", thoughtSignature = "empty+token==" };
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([tail])));
        var finished = parser.Complete();
        var continuation = Assert.IsType<ModelContinuation>(Assert.Single(finished.OfType<ModelCompleted>()).Continuation);
        var calls = finished.OfType<ToolCallCompleted>().ToArray();
        Assert.Equal(["native-one", "native-two"], calls.Select(call => call.ProviderCallId));
        Assert.NotEqual(calls[0].CallId, calls[0].ProviderCallId);
        var expected = JsonSerializer.SerializeToElement(parts.Append(tail).ToArray());
        Assert.True(JsonElement.DeepEquals(expected, continuation.Payload.GetProperty("parts")));
        var assistant = new ModelMessage(ModelRole.Assistant, "visible", ToolCalls: calls.Select(call =>
            new ModelToolCall(call.CallId, call.ToolName, call.Arguments, call.ProviderCallId)).ToArray(), Continuation: continuation);
        var messages = new[] { assistant }.Concat(calls.Select(call =>
            new ModelMessage(ModelRole.Tool, "result", call.CallId, call.ToolName))).ToArray();

        using var output = JsonDocument.Parse(GeminiRequestBuilder.Build(request with { Messages = messages }, names));

        var contents = output.RootElement.GetProperty("contents");
        Assert.True(JsonElement.DeepEquals(expected, contents[0].GetProperty("parts")));
        Assert.Equal(2, contents.GetArrayLength());
        Assert.Equal("native-one", contents[1].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.Equal("native-two", contents[1].GetProperty("parts")[1].GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.DoesNotContain("token", JsonSerializer.Serialize(assistant));
        Assert.DoesNotContain("token", assistant.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ForeignScope_IsRejectedWithoutEchoingOpaquePayload(int mismatch)
    {
        var request = GeminiStreamTestData.Request();
        var continuation = new ModelContinuation(mismatch == 0 ? Guid.NewGuid() : request.Provider.Id,
            mismatch == 1 ? ProviderProtocol.OpenAiResponses : request.Provider.Protocol,
            mismatch == 2 ? "other-model" : request.Model.ModelId,
            JsonSerializer.SerializeToElement(new { parts = new[] { new { text = "opaque-secret" } } }));
        request = request with { Messages = [new(ModelRole.Assistant, "answer", Continuation: continuation)] };

        var failure = Assert.Throws<InvalidOperationException>(() => GeminiRequestBuilder.Build(request, new ProviderToolNameMap([])));

        Assert.DoesNotContain("opaque-secret", failure.Message);
    }

    [Theory]
    [InlineData(ModelRole.User)]
    [InlineData(ModelRole.ToolObservation)]
    public void NonAssistantRoles_DoNotActivateNativeContinuation(ModelRole role)
    {
        var request = GeminiStreamTestData.Request();
        var continuation = new ModelContinuation(request.Provider.Id, request.Provider.Protocol, request.Model.ModelId,
            JsonSerializer.SerializeToElement(new { parts = new[] { SignedCall("native-one") } }));
        request = request with { Messages = [new(role, "ordinary data", Continuation: continuation)] };

        using var output = JsonDocument.Parse(GeminiRequestBuilder.Build(request, new ProviderToolNameMap(request.Tools)));

        var part = Assert.Single(output.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray());
        Assert.Equal("ordinary data", part.GetProperty("text").GetString());
        Assert.False(part.TryGetProperty("functionCall", out _));
    }

    [Fact]
    public void FailureAfterSignedStop_DiscardsContinuationAndEveryCall()
    {
        var request = GeminiStreamTestData.Request();
        var parser = new GeminiStreamParser(new ProviderToolNameMap(request.Tools), request);
        parser.Parse(GeminiStreamTestData.Data([SignedCall("native-one")], "STOP"));

        var failure = parser.Parse("{");

        Assert.Single(failure.OfType<ModelFailed>());
        Assert.Empty(parser.Complete());
        Assert.Empty(failure.OfType<ToolCallCompleted>());
        Assert.Empty(failure.OfType<ModelCompleted>());
    }

    [Theory]
    [InlineData("opaque+token==")]
    [InlineData("opaque\\\"签名\n🧩")]
    public void SignatureDecodingRetainsTheExactOpaqueValue(string signature)
    {
        var request = GeminiStreamTestData.Request();
        var parser = new GeminiStreamParser(new ProviderToolNameMap(request.Tools), request);
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([SignedCall("native", signature)], "STOP")));

        var completed = parser.Complete();

        var continuation = Assert.IsType<ModelContinuation>(Assert.Single(completed.OfType<ModelCompleted>()).Continuation);
        Assert.Equal(signature, continuation.Payload.GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
    }

    internal static object SignedCall(string identity, object? signature = null) => new
    {
        functionCall = new { name = GeminiStreamTestData.WireName, args = new { }, id = identity },
        thoughtSignature = signature ?? "call+token=="
    };
}
