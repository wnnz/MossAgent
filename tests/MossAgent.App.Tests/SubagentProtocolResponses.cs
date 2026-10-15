using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal static class SubagentProtocolResponses
{
    public const string Evidence = "AUTHORIZED_CHILD_FILE_EVIDENCE";
    public const string PrivateEvidence = "OUTSIDE_CONTENT_MUST_NOT_LEAK";
    public const string ParentMarker = "PARENT_CONTEXT_MARKER";
    public const string ChildReply = "## 子代理结果\n\n已读取授权文件，工具调用完成。";
    public const string ParentReply = "父任务已接收子代理结果。";

    public static string Parent(JsonElement request, int turn) => turn == 0
        ? Frames(new
        {
            type = "response.output_item.done",
            item = new
            {
                type = "function_call",
                call_id = "parent-delegate",
                name = ToolName(request, ProviderProtocol.OpenAiResponses, "subagent_run__"),
                arguments = JsonSerializer.Serialize(new { agent = "protocol-review", task = "仅检查合成文件" })
            }
        })
        : Frames(new { type = "response.output_text.delta", delta = ParentReply });

    public static string Child(JsonElement request, int turn, ProviderProtocol protocol, string path)
    {
        if (turn > 0) return Reply(protocol);
        var name = ToolName(request, protocol, "filesystem_read_file__");
        return protocol switch
        {
            ProviderProtocol.OpenAiResponses => Frames(ResponseCall(name, "child-read", path)),
            ProviderProtocol.OpenAiCompatible => Frames(new
            {
                choices = new[] { new { index = 0, delta = new { tool_calls = new[]
                {
                    new { index = 0, id = "child-read", type = "function", function = new { name, arguments = JsonSerializer.Serialize(new { path }) } }
                } }, finish_reason = "tool_calls" } }
            }),
            ProviderProtocol.GeminiGenerateContent => Frames(new
            {
                candidates = new[] { new { index = 0, content = new { parts = new[]
                {
                    new { functionCall = new { id = "child-read", name, args = new { path } }, thoughtSignature = "c2lnbmVkLWFsbG93ZWQ=" }
                } }, finishReason = "STOP" } }
            }),
            ProviderProtocol.AnthropicMessages => AnthropicTools(name, path),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };
    }

    private static object ResponseCall(string name, string id, string path) => new
    {
        type = "response.output_item.done",
        item = new { type = "function_call", call_id = id, name, arguments = JsonSerializer.Serialize(new { path }) }
    };

    private static string Reply(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses => Frames(new { type = "response.output_text.delta", delta = ChildReply }),
        ProviderProtocol.OpenAiCompatible => Frames(new { choices = new[] { new { index = 0, delta = new { content = ChildReply }, finish_reason = "stop" } } }),
        ProviderProtocol.GeminiGenerateContent => Frames(new { candidates = new[] { new { index = 0, content = new { parts = new[] { new { text = ChildReply } } }, finishReason = "STOP" } } }),
        ProviderProtocol.AnthropicMessages => SerializeFrames([
            new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = ChildReply } },
            new { type = "message_stop" }]),
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };

    private static string AnthropicTools(string name, string path) => SerializeFrames([
        new { type = "content_block_start", index = 0, content_block = new { type = "tool_use", id = "child-read", name, input = new { } } },
        new { type = "content_block_delta", index = 0, delta = new { type = "input_json_delta", partial_json = JsonSerializer.Serialize(new { path }) } },
        new { type = "content_block_stop", index = 0 },
        new { type = "message_stop" }]);

    private static string ToolName(JsonElement request, ProviderProtocol protocol, string prefix)
    {
        var tools = request.GetProperty("tools");
        if (protocol == ProviderProtocol.GeminiGenerateContent) tools = tools[0].GetProperty("functionDeclarations");
        return tools.EnumerateArray().Select(tool => protocol == ProviderProtocol.OpenAiCompatible
                ? tool.GetProperty("function").GetProperty("name").GetString()!
                : tool.GetProperty("name").GetString()!)
            .Single(name => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string Frames(params object[] values) =>
        SerializeFrames(values) + "data: [DONE]\n\n";

    private static string SerializeFrames(IEnumerable<object> values) =>
        string.Concat(values.Select(value => "data: " + JsonSerializer.Serialize(value) + "\n\n"));
}
