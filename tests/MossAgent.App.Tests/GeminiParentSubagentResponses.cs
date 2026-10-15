using System.Text.Json;

namespace MossAgent.App.Tests;

internal static class GeminiParentSubagentResponses
{
    public const string Signature = "c2lnbmVkLXBhcmVudA==";
    public const string NativeCallId = "gemini-parent-delegate";

    public static string Parent(JsonElement request, int turn)
    {
        object part;
        if (turn == 0)
        {
            var name = request.GetProperty("tools")[0].GetProperty("functionDeclarations").EnumerateArray()
                .Select(tool => tool.GetProperty("name").GetString()!)
                .Single(value => value.StartsWith("subagent_run__", StringComparison.Ordinal));
            part = new
            {
                functionCall = new { id = NativeCallId, name, args = new { agent = "protocol-review", task = "仅检查合成文件" } },
                thoughtSignature = Signature
            };
        }
        else part = new { text = SubagentProtocolResponses.ParentReply };
        return "data: " + JsonSerializer.Serialize(new
        {
            candidates = new[] { new { index = 0, content = new { parts = new[] { part } }, finishReason = "STOP" } }
        }) + "\n\ndata: [DONE]\n\n";
    }

    public static JsonElement[] ReadParts(JsonElement request) => request.GetProperty("contents").EnumerateArray()
        .SelectMany(message => message.GetProperty("parts").EnumerateArray()).ToArray();

    public static JsonElement ReadOutput(JsonElement request) => ReadParts(request)
        .Single(part => part.TryGetProperty("functionResponse", out _))
        .GetProperty("functionResponse").GetProperty("response");
}
