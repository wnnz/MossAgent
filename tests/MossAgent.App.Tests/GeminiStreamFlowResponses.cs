using System.Text.Json;

namespace MossAgent.App.Tests;

internal static class GeminiStreamFlowResponses
{
    public const string Evidence = "Gemini 文件证据：中文与 Unicode 🔎。";
    public const string PartialReply = "已收到 Gemini 的流式片段。";
    public const string FinalReply = "Gemini 工具链路已完成。";
    public const string RejectedContent = "不应执行的 Gemini 候选写入。";

    public static string Create(JsonElement request, int turn, string path, GeminiStreamScenario scenario)
    {
        if (scenario >= GeminiStreamScenario.SignedSteps)
            return GeminiSignatureFlowResponses.Create(request, turn, path, scenario);
        if (turn > 0) return Chunk(Candidate(0, [new { text = FinalReply }], "STOP"));
        var prefix = Chunk(Candidate(0, [new { text = PartialReply }]));
        var read = WireName(request, "filesystem.read_file");
        var write = WireName(request, "filesystem.write_file");
        var mutation = Call(write, new { path, content = RejectedContent });
        var stopped = Chunk(Candidate(0, [mutation], "STOP"));
        return prefix + (scenario switch
        {
            GeminiStreamScenario.SelectedCandidate => Chunk(
                Candidate(1, [new { text = "不能合并的替代候选。" }, mutation], "STOP"),
                Candidate(0, [Call(read, new { path })], "STOP"))
                + "data: {\"candidates\":[],\"usageMetadata\":{\"totalTokenCount\":12}}\n\n",
            GeminiStreamScenario.PrematureEof => Chunk(Candidate(0, [mutation])),
            GeminiStreamScenario.BlockedFinish => Chunk(Candidate(0, [mutation], "SAFETY")),
            GeminiStreamScenario.BlockedPrompt => Chunk(Candidate(0, [mutation]))
                + "data: {\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}\n\n",
            GeminiStreamScenario.OutputLimit => Chunk(Candidate(0, [mutation], "MAX_TOKENS")),
            GeminiStreamScenario.MalformedAfterStop => stopped + "data: {\n\n",
            GeminiStreamScenario.LateText => stopped + Chunk(Candidate(0, [new { text = "迟到正文。" }])),
            GeminiStreamScenario.LateTool => stopped + Chunk(Candidate(0, [mutation])),
            GeminiStreamScenario.InvalidSecondToolArguments => Chunk(Candidate(0, [mutation, Call(read, "invalid")], "STOP")),
            GeminiStreamScenario.OversizedLineAfterStop => stopped + "data: " + new string('x', 1024 * 1024) + "\n\n",
            GeminiStreamScenario.OversizedEventAfterStop => stopped
                + string.Concat(Enumerable.Repeat("data:" + new string('x', 1023) + "\n", 2050)) + "\n",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        });
    }

    private static object Candidate(int index, object[] parts, string? finish = null) =>
        new { index, content = new { role = "model", parts }, finishReason = finish };

    private static object Call(string name, object args) => new { functionCall = new { name, args } };

    private static string Chunk(params object[] candidates) =>
        $"data: {JsonSerializer.Serialize(new { candidates })}\n\n";

    internal static string WireName(JsonElement request, string name) => request.GetProperty("tools")[0]
        .GetProperty("functionDeclarations").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!)
        .Single(wireName => wireName.StartsWith(name.Replace('.', '_') + "__", StringComparison.Ordinal));
}
