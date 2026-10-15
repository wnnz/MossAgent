using System.Text.Json;

namespace MossAgent.App.Tests;

internal static class GeminiSignatureFlowResponses
{
    public const string InternalThought = "不应展示或持久化的内部推理。";

    public static string Create(JsonElement request, int turn, string path, GeminiStreamScenario scenario)
    {
        if (scenario is GeminiStreamScenario.SignedSteps or GeminiStreamScenario.SignedParallelSteps)
        {
            var parallel = scenario == GeminiStreamScenario.SignedParallelSteps;
            if (turn > 0 && !HasValidHistory(request, turn, path, parallel))
                return "data: {\"error\":{\"code\":400,\"message\":\"Invalid signed history\"}}\n\n";
            var parts = Parts(request, turn, path, parallel);
            return turn < 2 ? Chunk(parts[..^1], "STOP") + Chunk([parts[^1]])
                : Chunk([new { text = GeminiStreamFlowResponses.FinalReply }], "STOP");
        }
        if (turn > 0) return Chunk([new { text = GeminiStreamFlowResponses.FinalReply }], "STOP");
        var call = new Dictionary<string, object?>
        {
            ["name"] = GeminiStreamFlowResponses.WireName(request, "filesystem.write_file"),
            ["args"] = new { path, content = GeminiStreamFlowResponses.RejectedContent },
            ["id"] = scenario == GeminiStreamScenario.InvalidNativeCallId ? new { invalid = true } : "native-id"
        };
        var part = new Dictionary<string, object?> { ["functionCall"] = call };
        if (scenario != GeminiStreamScenario.MissingRequiredSignature)
            part["thoughtSignature"] = scenario switch
            {
                GeminiStreamScenario.InvalidSignature => new { invalid = true },
                GeminiStreamScenario.EmptySignature => "",
                GeminiStreamScenario.OversizedSignature => new string('s', 131073),
                _ => "valid-signature"
            };
        return Chunk([new { text = GeminiStreamFlowResponses.PartialReply }])
            + Chunk(scenario == GeminiStreamScenario.DuplicateNativeCallId ? [part, part] : [part], "STOP");
    }

    public static object[] Parts(JsonElement request, int step, string path, bool parallel = false)
    {
        List<object> parts =
        [
        new { text = InternalThought, thought = true, thoughtSignature = $"thought-signature-{step}" },
        new { text = $"读取步骤 {step + 1}。", thoughtSignature = $"text-signature-{step}" },
        new
        {
            functionCall = new
            {
                name = GeminiStreamFlowResponses.WireName(request, "filesystem.read_file"),
                args = new { path }, id = $"native-call-{step}"
            },
            thoughtSignature = $"call-signature-{step}"
        }
        ];
        if (parallel) parts.Add(new
        {
            functionCall = new
            {
                name = GeminiStreamFlowResponses.WireName(request, "filesystem.read_file"),
                args = new { path },
                id = $"native-parallel-{step}"
            }
        });
        parts.Add(new { text = "", thoughtSignature = $"tail-signature-{step}" });
        return parts.ToArray();
    }

    private static bool HasValidHistory(JsonElement request, int turn, string path, bool parallel)
    {
        var contents = request.GetProperty("contents").EnumerateArray().ToArray();
        var models = contents.Where(content => content.GetProperty("role").GetString() == "model").ToArray();
        if (models.Length != turn) return false;
        for (var step = 0; step < turn; step++)
            if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(Parts(request, step, path, parallel)), models[step].GetProperty("parts")))
                return false;
        var responseGroups = contents.Where(content => content.GetProperty("parts").EnumerateArray()
            .Any(part => part.TryGetProperty("functionResponse", out _))).ToArray();
        if (responseGroups.Length != turn) return false;
        for (var step = 0; step < turn; step++)
        {
            var parts = responseGroups[step].GetProperty("parts");
            if (parts.GetArrayLength() != (parallel ? 2 : 1)) return false;
            if (!MatchesIdentity(parts[0], $"native-call-{step}")) return false;
            if (parallel && !MatchesIdentity(parts[1], $"native-parallel-{step}")) return false;
        }
        return true;
    }

    private static bool MatchesIdentity(JsonElement part, string expected) => part.GetProperty("functionResponse")
        .TryGetProperty("id", out var identity) && identity.GetString() == expected;

    private static string Chunk(object[] parts, string? finish = null) => "data: " + JsonSerializer.Serialize(new
    {
        candidates = new[] { new { index = 0, content = new { role = "model", parts }, finishReason = finish } }
    }) + "\n\n";
}
