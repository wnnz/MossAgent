using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ModelContinuationTests
{
    [Fact]
    public void Snapshot_OwnsItsPayloadAndDoesNotExposeItInDiagnostics()
    {
        ModelContinuation continuation;
        using (var document = JsonDocument.Parse("{\"parts\":[{\"thoughtSignature\":\"opaque-secret\"}]}"))
            continuation = new ModelContinuation(Guid.NewGuid(), ProviderProtocol.GeminiGenerateContent, "gemini-3", document.RootElement);

        Assert.Equal("opaque-secret", continuation.Payload.GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        Assert.Equal(continuation.Payload.GetRawText().Length, continuation.CharacterCount);
        Assert.DoesNotContain("opaque-secret", continuation.ToString());
        Assert.DoesNotContain("opaque-secret", JsonSerializer.Serialize(continuation));
        Assert.DoesNotContain("opaque-secret", JsonSerializer.Serialize(new ModelCompleted(Continuation: continuation)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"opaque-secret\"")]
    public void InvalidPayloadShape_IsRejectedWithoutEchoingIt(string payload)
    {
        using var document = JsonDocument.Parse(payload);

        var failure = Assert.Throws<ArgumentException>(() => new ModelContinuation(Guid.NewGuid(),
            ProviderProtocol.GeminiGenerateContent, "gemini-3", document.RootElement));

        Assert.DoesNotContain("opaque-secret", failure.Message);
    }

    [Fact]
    public void OversizedPayload_IsRejected()
    {
        var payload = JsonSerializer.SerializeToElement(new { token = new string('x', ModelContinuation.MaximumCharacters) });

        Assert.Throws<ArgumentException>(() => new ModelContinuation(Guid.NewGuid(), ProviderProtocol.GeminiGenerateContent, "gemini-3", payload));
    }

    [Fact]
    public void ContextBudget_PreservesSignedContentAndTrimsOnlyOrdinaryResultData()
    {
        var continuation = Continuation("signature");
        var signed = new ModelMessage(ModelRole.Assistant, "visible answer", Continuation: continuation);
        var result = ModelContextWindowBuilder.Build([new(ModelRole.User, "read"), signed,
            new(ModelRole.Tool, new string('x', 6000), "call", "test.read")], [], Model(1000));

        var preserved = Assert.Single(result, message => message.Role == ModelRole.Assistant);
        Assert.Same(continuation, preserved.Continuation);
        Assert.Equal(signed.Content, preserved.Content);
        Assert.True(Assert.Single(result, message => message.Role == ModelRole.Tool).Content.Length < 6000);
        Assert.True(ModelContextWindowBuilder.Estimate([signed]) > signed.Content.Length + continuation.CharacterCount);
    }

    [Fact]
    public void LatestOpaqueMetadataThatCannotFit_IsRejectedWithoutTruncatingItsSignature()
    {
        var signed = new ModelMessage(ModelRole.Assistant, "visible", Continuation: Continuation(new string('s', 4000)));

        Assert.Throws<InvalidOperationException>(() => ModelContextWindowBuilder.Build([new(ModelRole.User, "read"), signed], [], Model(800)));
        Assert.Equal(4000, signed.Continuation!.Payload.GetProperty("parts")[0].GetProperty("thoughtSignature").GetString()!.Length);
    }

    [Fact]
    public void SharedSubagentText_DoesNotIncludeNativeContinuationOrSignatures()
    {
        var context = SubagentConversationContextBuilder.Build([new(ModelRole.User, "question"),
            new(ModelRole.Assistant, "visible", Continuation: Continuation("opaque-secret"))]);

        Assert.NotNull(context);
        Assert.Contains("visible", context.Text);
        Assert.DoesNotContain("opaque-secret", context.Text);
        Assert.DoesNotContain("thoughtSignature", context.Text);
    }

    private static ModelContinuation Continuation(string signature) => new(Guid.NewGuid(), ProviderProtocol.GeminiGenerateContent,
        "gemini-3", JsonSerializer.SerializeToElement(new { parts = new[] { new { text = "visible", thoughtSignature = signature } } }));

    private static ModelProfile Model(int contextLength) => new(Guid.NewGuid(), Guid.NewGuid(), "gemini-3", "fixture",
        contextLength, 128, "high", null, null, false, false, true, true, "{}");
}
