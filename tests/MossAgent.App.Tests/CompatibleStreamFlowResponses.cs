using System.Text.Json;

namespace MossAgent.App.Tests;

internal static class CompatibleStreamFlowResponses
{
    public const string Evidence = "兼容接口文件证据：中文与 Unicode 🔎。";
    public const string PartialReply = "已收到兼容接口的流式片段。";
    public const string FinalReply = "兼容接口链路已完成。";
    public const string RejectedContent = "不应执行的候选写入。";

    public static string Create(JsonElement request, int turn, string path, CompatibleStreamScenario scenario)
    {
        if (turn > 0) return Chunk(Choice(0, new { content = FinalReply }, "stop")) + "data: [DONE]\n\n";
        var prefix = Chunk(Choice(0, new { content = PartialReply }));
        var write = WireName(request, "filesystem.write_file");
        var read = WireName(request, "filesystem.read_file");
        var mutation = Call(0, "pending-write", write, JsonSerializer.Serialize(new { path, content = RejectedContent }));
        return scenario switch
        {
            CompatibleStreamScenario.PrematureEof => prefix + Chunk(Choice(0, new { tool_calls = new[] { mutation } })),
            CompatibleStreamScenario.MalformedAfterToolFinish => prefix
                + Chunk(Choice(0, new { tool_calls = new[] { mutation } }, "tool_calls")) + "data: {\n\n",
            CompatibleStreamScenario.InvalidSecondToolArguments => prefix + Chunk(Choice(0,
                new { tool_calls = new[] { mutation, Call(1, "invalid-read", read, "{") } }, "tool_calls")),
            CompatibleStreamScenario.MultipleChoices => prefix + MultipleChoices(read, write, path),
            CompatibleStreamScenario.OversizedLineAfterToolFinish => prefix
                + Chunk(Choice(0, new { tool_calls = new[] { mutation } }, "tool_calls"))
                + "data: " + new string('x', 1024 * 1024) + "\n\n",
            CompatibleStreamScenario.OversizedEventAfterToolFinish => prefix
                + Chunk(Choice(0, new { tool_calls = new[] { mutation } }, "tool_calls"))
                + string.Concat(Enumerable.Repeat("data:" + new string('x', 1023) + "\n", 2050)) + "\n",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }

    private static string MultipleChoices(string read, string write, string path) => Chunk(
        Choice(0, new { tool_calls = new[] { Call(0, "chosen-read", read, JsonSerializer.Serialize(new { path })) } }, "tool_calls"),
        Choice(1, new
        {
            content = "不能合并的替代候选。",
            tool_calls = new[] { Call(0, "alternate-write", write, JsonSerializer.Serialize(new { path, content = RejectedContent })) }
        }, "tool_calls"))
        + Chunk(Choice(0, new { content = "", tool_calls = Array.Empty<object>() }))
        + "data: {\"choices\":[],\"usage\":{\"total_tokens\":12}}\n\ndata: [DONE]\n\n";

    private static object Choice(int index, object delta, string? finish = null) =>
        new { index, delta, finish_reason = finish };

    private static object Call(int index, string id, string name, string arguments) =>
        new { index, id, type = "function", function = new { name, arguments } };

    private static string Chunk(params object[] choices) =>
        $"data: {JsonSerializer.Serialize(new { id = "compatible-flow", choices })}\n\n";

    private static string WireName(JsonElement request, string name) => request.GetProperty("tools").EnumerateArray()
        .Select(tool => tool.GetProperty("function").GetProperty("name").GetString()!)
        .Single(wireName => wireName.StartsWith(name.Replace('.', '_') + "__", StringComparison.Ordinal));
}
