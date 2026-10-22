using System.Text.Json;

namespace MossAgent.Providers.Streaming;

/// <summary>
/// Parses one SSE data payload into JSON without letting a malformed frame escape as an exception.
/// Providers turn a null result into a deterministic <c>invalid_response</c> failure so a broken
/// connection or proxy response cannot abort the run with an unexpected exception type.
/// </summary>
internal static class ProviderFrameJson
{
    /// <summary>
    /// Parses a single SSE data payload.
    /// </summary>
    /// <param name="data">Raw payload after the <c>data:</c> prefix; must already be UTF-8 decoded.</param>
    /// <returns>The parsed document, or null when the payload is not valid JSON.</returns>
    public static JsonDocument? TryParse(string data)
    {
        try { return JsonDocument.Parse(data); }
        catch (JsonException) { return null; }
    }
}
