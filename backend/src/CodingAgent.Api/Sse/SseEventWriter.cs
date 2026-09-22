using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingAgent.Api.Sse;

/// <summary>SSE 事件写出（event: name\ndata: json\n\n）。</summary>
public static class SseEventWriter
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new LowerSnakeCaseEnumConverterFactory() },
    };

    public static async Task WriteAsync(HttpResponse response, string eventName, object data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
