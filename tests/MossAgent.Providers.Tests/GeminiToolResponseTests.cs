using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiToolResponseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("plain tool output")]
    [InlineData("{\"Content\":\"cropped")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("null")]
    public void CroppedLegacyOrNonObjectToolOutputIsEncodedAsDataInsteadOfBreakingTheRequest(string output)
    {
        using var document = JsonDocument.Parse(Build(output));
        var response = document.RootElement.GetProperty("contents")[0].GetProperty("parts")[0]
            .GetProperty("functionResponse").GetProperty("response");
        Assert.Equal(JsonValueKind.Object, response.ValueKind);
        Assert.Equal(output, response.GetProperty("output").GetString());
    }

    [Fact]
    public void ValidStructuredNativeResultRetainsItsFields()
    {
        using var document = JsonDocument.Parse(Build("{\"IsSuccess\":true,\"Content\":\"read evidence\"}"));
        var response = document.RootElement.GetProperty("contents")[0].GetProperty("parts")[0]
            .GetProperty("functionResponse").GetProperty("response");
        Assert.True(response.GetProperty("IsSuccess").GetBoolean());
        Assert.Equal("read evidence", response.GetProperty("Content").GetString());
        Assert.False(response.TryGetProperty("output", out _));
    }

    private static string Build(string output)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.GeminiGenerateContent,
            new Uri("https://fixture.example.test/"), "synthetic-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "gemini-3-fixture", "fixture", 32000, 1024,
            "low", null, null, false, false, true, true, "{}");
        var tool = new ToolDescriptor("fixture.read", "fixture", "{\"type\":\"object\"}", ToolRiskLevel.ReadOnly, ToolCapability.FileRead);
        var request = new ModelRequest(provider, model, [new(ModelRole.Tool, output, "call", tool.Name)], [tool]);
        return GeminiRequestBuilder.Build(request, new ProviderToolNameMap([tool]));
    }
}
