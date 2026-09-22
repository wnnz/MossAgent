using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Llm;

/// <summary>Anthropic 协议（/v1/messages SSE 流式；system 顶格，tool_use/tool_result 块）。</summary>
public class AnthropicClient(HttpClient http, Provider provider) : ILlmClient
{
    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildBody(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiCompatibleClient.CombineUrl(provider.BaseUrl, "/v1/messages"));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        httpRequest.Headers.Add("x-api-key", provider.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        string? sendError = null;
        HttpResponseMessage? response = null;
        try
        {
            response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            sendError = $"LLM 请求失败: {ex.Message}";
        }

        if (sendError is not null || response is null)
        {
            yield return LlmStreamEvent.ErrorOf(sendError ?? "LLM 请求失败");
            yield break;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                yield return LlmStreamEvent.ErrorOf($"LLM 返回 {(int)response.StatusCode}: {error[..Math.Min(error.Length, 500)]}");
                yield break;
            }

            var currentCallId = string.Empty;
            var currentCallName = string.Empty;
            var currentArgs = new StringBuilder();

            await foreach (var line in SseLineReader.ReadLinesAsync(response, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }
                var data = line["data:".Length..].Trim();
                if (data.Length == 0)
                {
                    continue;
                }

                JsonElement evt;
                try
                {
                    evt = JsonDocument.Parse(data).RootElement.Clone();
                }
                catch (JsonException)
                {
                    continue;
                }

                var type = evt.TryGetProperty("type", out var t) ? t.GetString() : null;
                switch (type)
                {
                    case "content_block_start" when evt.TryGetProperty("content_block", out var block):
                    {
                        var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : null;
                        if (blockType == "tool_use")
                        {
                            // 先冲刷上一个工具调用
                            if (!string.IsNullOrEmpty(currentCallId))
                            {
                                yield return LlmStreamEvent.ToolCallOf(new LlmToolCall(currentCallId, currentCallName, currentArgs.ToString()));
                                currentCallId = string.Empty;
                                currentArgs.Clear();
                            }
                            currentCallId = block.TryGetProperty("id", out var bid) ? bid.GetString() ?? string.Empty : string.Empty;
                            currentCallName = block.TryGetProperty("name", out var bn) ? bn.GetString() ?? string.Empty : string.Empty;
                        }
                        break;
                    }
                    case "content_block_delta" when evt.TryGetProperty("delta", out var delta):
                    {
                        var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;
                        if (deltaType == "text_delta" && delta.TryGetProperty("text", out var text))
                        {
                            yield return LlmStreamEvent.DeltaOf(text.GetString() ?? string.Empty);
                        }
                        else if (deltaType == "input_json_delta" && delta.TryGetProperty("partial_json", out var pj))
                        {
                            currentArgs.Append(pj.GetString());
                        }
                        break;
                    }
                    case "content_block_stop":
                    {
                        if (!string.IsNullOrEmpty(currentCallId))
                        {
                            yield return LlmStreamEvent.ToolCallOf(new LlmToolCall(currentCallId, currentCallName, currentArgs.ToString()));
                            currentCallId = string.Empty;
                            currentArgs.Clear();
                        }
                        break;
                    }
                    case "message_delta" when evt.TryGetProperty("delta", out var delta):
                    {
                        if (delta.TryGetProperty("stop_reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                        {
                            yield return LlmStreamEvent.CompletedOf(reason.GetString());
                        }
                        break;
                    }
                    case "error":
                    {
                        var message = evt.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg)
                            ? msg.GetString() ?? "未知错误"
                            : "未知错误";
                        yield return LlmStreamEvent.ErrorOf(message);
                        yield break;
                    }
                }
            }
        }
    }

    internal static object BuildBody(LlmRequest request)
    {
        string? system = null;
        var messages = new List<object>();
        var pendingToolResults = new List<object>();

        void FlushToolResults()
        {
            if (pendingToolResults.Count > 0)
            {
                messages.Add(new { role = "user", content = pendingToolResults.ToList() });
                pendingToolResults.Clear();
            }
        }

        foreach (var m in request.Messages)
        {
            switch (m.Role)
            {
                case "system":
                    system = string.Concat(system ?? string.Empty, m.Content ?? string.Empty);
                    break;
                case "assistant" when m.ToolCalls is { Count: > 0 }:
                    FlushToolResults();
                    var blocks = new List<object>();
                    if (!string.IsNullOrEmpty(m.Content))
                    {
                        blocks.Add(new { type = "text", text = m.Content });
                    }
                    blocks.AddRange(m.ToolCalls.Select(c => new
                    {
                        type = "tool_use",
                        id = c.Id,
                        name = c.Name,
                        input = c.ParseArguments(),
                    }));
                    messages.Add(new { role = "assistant", content = blocks });
                    break;
                case "tool":
                    pendingToolResults.Add(new { type = "tool_result", tool_use_id = m.ToolCallId, content = m.Content ?? string.Empty });
                    break;
                default:
                    FlushToolResults();
                    messages.Add(new { role = m.Role, content = m.Content ?? string.Empty });
                    break;
            }
        }
        FlushToolResults();

        var maxTokens = request.MaxOutputTokens;
        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["max_tokens"] = maxTokens,
            ["messages"] = messages,
            ["stream"] = true,
        };
        if (!string.IsNullOrEmpty(system))
        {
            body["system"] = system;
        }

        if (request.Tools is { Count: > 0 })
        {
            body["tools"] = request.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.ParseParametersSchema(),
            }).ToList();
        }

        // Anthropic 扩展思考：budget 必须 < max_tokens
        if (request.ReasoningEffort != Domain.Enums.ReasoningEffort.Off)
        {
            var budget = request.ReasoningEffort switch
            {
                Domain.Enums.ReasoningEffort.Low => 2048,
                Domain.Enums.ReasoningEffort.Medium => 8192,
                Domain.Enums.ReasoningEffort.High => 16384,
                _ => 2048,
            };
            if (budget >= maxTokens)
            {
                body["max_tokens"] = budget + 4096;
            }
            body["thinking"] = new { type = "enabled", budget_tokens = budget };
        }

        return body;
    }
}
