using System.Net;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class StreamingErrorSanitizationTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task StreamAsync_RedactsCredentialsFromSseFailures(ProviderProtocol protocol)
    {
        const string apiKey = "fixture-api-secret";
        const string headerSecret = "fixture-header-secret";
        var code = $"error-{apiKey}-{headerSecret}";
        var message = $"Rejected {apiKey} and {headerSecret}\r\n{new string('x', 600)}";
        var body = $"data: {CreateErrorPayload(protocol, code, message)}\n\n";
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, body,
            contentType: "text/event-stream");
        var modelProvider = CreateProvider(protocol, handler);
        var request = CreateRequest(protocol, apiKey,
            new Dictionary<string, string> { ["X-Trace-Secret"] = headerSecret });
        var events = new List<ModelEvent>();

        await foreach (var modelEvent in modelProvider.StreamAsync(
                           request, TestContext.Current.CancellationToken))
        {
            events.Add(modelEvent);
        }

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        AssertRedacted(failure, apiKey, headerSecret);
    }

    private static void AssertRedacted(ModelFailed failure, string apiKey, string headerSecret)
    {
        foreach (var value in new[] { failure.Code, failure.Message })
        {
            Assert.DoesNotContain(apiKey, value, StringComparison.Ordinal);
            Assert.DoesNotContain(headerSecret, value, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', value);
            Assert.DoesNotContain('\n', value);
            Assert.True(value.Length <= 501);
        }
        Assert.Contains("***", failure.Message, StringComparison.Ordinal);
    }

    private static string CreateErrorPayload(ProviderProtocol protocol, string code, string message) =>
        protocol switch
        {
            ProviderProtocol.OpenAiResponses => JsonSerializer.Serialize(new
            {
                type = "error",
                error = new { code, message }
            }),
            ProviderProtocol.AnthropicMessages => JsonSerializer.Serialize(new
            {
                type = "error",
                error = new { type = code, message }
            }),
            _ => JsonSerializer.Serialize(new { error = new { code, message } })
        };

}
