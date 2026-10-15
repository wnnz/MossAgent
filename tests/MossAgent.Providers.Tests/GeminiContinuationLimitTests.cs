using MossAgent.Application.Models;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiContinuationLimitTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(42)]
    [InlineData(true)]
    public void InvalidSignature_IsRejectedWithoutReleasingCalls(object signature)
    {
        var parser = Parser();

        var events = parser.Parse(GeminiStreamTestData.Data([GeminiContinuationTests.SignedCall("native", signature)], "STOP"));

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
        Assert.Empty(parser.Complete());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignatureLimit_IsAppliedToIndividualAndAccumulatedParts(bool accumulated)
    {
        var parser = Parser();
        var signature = new string('s', GeminiContentBuffer.MaximumSignatureCharacters + (accumulated ? 0 : 1));
        IReadOnlyList<ModelEvent> events = [];
        for (var index = 0; index < (accumulated ? 5 : 1); index++)
            events = parser.Parse(GeminiStreamTestData.Data([GeminiContinuationTests.SignedCall($"native-{index}", signature)],
                accumulated ? null : "STOP"));

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
        Assert.Empty(parser.Complete());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContentBuffer_HasFinitePartAndCharacterBudgets(bool largeParts)
    {
        var parser = Parser();
        var text = largeParts ? new string('x', 128 * 1024) : "";
        IReadOnlyList<ModelEvent> events = [];
        for (var index = 0; index < (largeParts ? 65 : GeminiContentBuffer.MaximumParts + 1); index++)
        {
            events = parser.Parse(GeminiStreamTestData.Data([new { text }]));
            if (parser.IsTerminal) break;
        }

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
        Assert.Empty(parser.Complete());
    }

    [Fact]
    public void DuplicateNativeIdsAcrossFrames_InvalidateTheWholeBatch()
    {
        var parser = Parser();
        Assert.Empty(parser.Parse(GeminiStreamTestData.Data([GeminiContinuationTests.SignedCall("native")])));

        var events = parser.Parse(GeminiStreamTestData.Data([GeminiContinuationTests.SignedCall("native")], "STOP"));

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
        Assert.Empty(parser.Complete());
    }

    [Fact]
    public void LateHiddenThought_IsNotAllowedToChangeCompletedContent()
    {
        var parser = Parser();
        parser.Parse(GeminiStreamTestData.Data([GeminiContinuationTests.SignedCall("native")], "STOP"));

        var events = parser.Parse(GeminiStreamTestData.Data([new { thought = true, text = "late internal" }]));

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullOptionalMetadata_DoesNotInventSignatureOrNativeIdentity(bool requiresSignature)
    {
        var request = GeminiStreamTestData.Request();
        if (requiresSignature) request = request with { Model = request.Model with { ModelId = "gemini-3-flash" } };
        var parser = new GeminiStreamParser(new ProviderToolNameMap(request.Tools), request);
        var events = parser.Parse(GeminiStreamTestData.Data([new
        {
            functionCall = new { name = GeminiStreamTestData.WireName, args = new { }, id = (string?)null },
            thoughtSignature = (string?)null
        }], "STOP"));

        if (requiresSignature) GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
        else
        {
            Assert.Empty(events);
            var completed = parser.Complete();
            Assert.Null(Assert.Single(completed.OfType<ToolCallCompleted>()).ProviderCallId);
            Assert.Null(Assert.Single(completed.OfType<ModelCompleted>()).Continuation);
        }
    }

    private static GeminiStreamParser Parser()
    {
        var request = GeminiStreamTestData.Request();
        return new GeminiStreamParser(new ProviderToolNameMap(request.Tools), request);
    }
}
