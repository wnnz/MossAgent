using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Providers.Tests;

internal static class GeminiStreamTestData
{
    public const string Secret = "gemini-fixture-secret";
    public static ToolDescriptor Tool => new("filesystem.write_file", "fixture", "{\"type\":\"object\"}",
        ToolRiskLevel.Mutation, ToolCapability.FileWrite);
    public static string WireName => new ProviderToolNameMap([Tool]).Encode(Tool.Name);

    public static string Frame(object[] parts, string? finish = null) =>
        "data: " + Data(parts, finish) + "\n\n";

    public static string Data(object[] parts, string? finish = null) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { index = 0, content = new { parts }, finishReason = finish } }
    });

    public static object Call(object? args = null) => new { functionCall = new { name = WireName, args = args ?? new { path = "fixture.txt" } } };

    public static ModelRequest Request()
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(providerId, "fixture", ProviderProtocol.GeminiGenerateContent,
            new Uri("https://example.test/v1beta/"), Secret, null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), providerId, "gemini-2.5-flash", "Fixture",
            8192, 1024, "off", null, null, false, false, true, true, "{}");
        return new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "hello")], [Tool]);
    }

    public static GeminiGenerateContentProvider Provider(ProviderByteResponseHandler handler)
    {
        var clients = new StaticProviderHttpClientFactory(handler);
        return new GeminiGenerateContentProvider(clients);
    }

    public static async Task<List<ModelEvent>> CollectAsync(string payload, CancellationToken token)
    {
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(payload));
        var result = new List<ModelEvent>();
        await foreach (var item in Provider(handler).StreamAsync(Request(), token)) result.Add(item);
        Xunit.Assert.True(handler.ResponseStream!.IsDisposed);
        return result;
    }
}
