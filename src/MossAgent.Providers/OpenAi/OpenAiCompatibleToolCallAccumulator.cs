using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal sealed class OpenAiCompatibleToolCallAccumulator(ProviderToolNameMap toolNames)
{
    internal const int MaximumCalls = 128;
    internal const int MaximumArgumentCharacters = 1024 * 1024;
    internal const int MaximumTotalArgumentCharacters = 4 * MaximumArgumentCharacters;
    internal const int MaximumIdentityCharacters = 512;
    private readonly Dictionary<int, (string? CallId, string? WireName, StringBuilder Arguments)> _calls = [];
    private int _argumentCharacters;

    public ModelFailed? Capture(JsonElement delta)
    {
        if (!delta.TryGetProperty("tool_calls", out var calls) || calls.ValueKind == JsonValueKind.Null) return null;
        if (calls.ValueKind != JsonValueKind.Array) return Failure("invalid_response", "CompatibleInvalidResponse");
        foreach (var call in calls.EnumerateArray())
        {
            if (CaptureCall(call) is { } failure) return failure;
        }
        return null;
    }

    public IReadOnlyList<ModelEvent> Drain()
    {
        var results = new List<ModelEvent>();
        foreach (var call in _calls.OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            if (string.IsNullOrWhiteSpace(call.CallId) || string.IsNullOrWhiteSpace(call.WireName))
                return [Failure("invalid_tool_arguments", "CompatibleIncompleteToolCall")];
            try
            {
                var text = call.Arguments.ToString();
                using var arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                results.Add(new ToolCallCompleted(call.CallId, toolNames.Decode(call.WireName), arguments.RootElement.Clone()));
            }
            catch (JsonException)
            {
                return [Failure("invalid_tool_arguments", "CompatibleInvalidToolArguments")];
            }
        }
        _calls.Clear();
        return results;
    }

    private ModelFailed? CaptureCall(JsonElement call)
    {
        if (call.ValueKind != JsonValueKind.Object) return Failure("invalid_response", "CompatibleInvalidResponse");
        var index = _calls.Count;
        if (call.TryGetProperty("index", out var value)
            && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out index) || index < 0))
            return Failure("invalid_response", "CompatibleInvalidResponse");
        if (!_calls.TryGetValue(index, out var current))
        {
            if (_calls.Count >= MaximumCalls) return Failure("tool_calls_limit", "CompatibleToolCallsLimit");
            current = (null, null, new StringBuilder());
        }
        if (ReadString(call, "id") is { } callId) current.CallId = callId;
        if (current.CallId?.Length > MaximumIdentityCharacters)
            return Failure("invalid_tool_arguments", "CompatibleToolIdentityLimit");
        if (call.TryGetProperty("function", out var function))
        {
            if (function.ValueKind != JsonValueKind.Object) return Failure("invalid_response", "CompatibleInvalidResponse");
            if (ReadString(function, "name") is { } name) current.WireName = name;
            if (current.WireName?.Length > MaximumIdentityCharacters)
                return Failure("invalid_tool_arguments", "CompatibleToolIdentityLimit");
            if (ReadString(function, "arguments") is { } arguments)
            {
                if (arguments.Length > MaximumArgumentCharacters - current.Arguments.Length
                    || arguments.Length > MaximumTotalArgumentCharacters - _argumentCharacters)
                    return Failure("tool_arguments_limit", "CompatibleToolArgumentsLimit");
                current.Arguments.Append(arguments);
                _argumentCharacters += arguments.Length;
            }
        }
        _calls[index] = current;
        return null;
    }

    private static ModelFailed Failure(string code, string resource) => new(code, ProviderText.Get(resource));

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
