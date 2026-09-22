using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Llm;

/// <summary>OpenAI 兼容协议（/v1/chat/completions SSE 流式），适用于 OpenAI/DeepSeek/Qwen/Ollama 等。</summary>
public class OpenAiCompatibleClient(HttpClient http, Provider provider) : ILlmClient
{
    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildBody(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CombineUrl(provider.BaseUrl, "/v1/chat/completions"));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(provider.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        }

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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sendError = "LLM 请求超时";
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
                yield return LlmStreamEvent.ErrorOf($"LLM 返回 {(int)response.StatusCode}: {Truncate(error)}");
                yield break;
            }

            // 流式累积 tool_calls 分片（按 index 合并 id/name/arguments）
            var pendingCalls = new Dictionary<int, LlmToolCall>();

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
                if (data == "[DONE]")
                {
                    break;
                }

                JsonElement chunk;
                try
                {
                    chunk = JsonDocument.Parse(data).RootElement.Clone();
                }
                catch (JsonException)
                {
                    continue;
                }

                if (!chunk.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    continue;
                }
                var choice = choices[0];

                if (choice.TryGetProperty("delta", out var delta))
                {
                    if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            yield return LlmStreamEvent.DeltaOf(text);
                        }
                    }

                    if (delta.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tc in toolCalls.EnumerateArray())
                        {
                            var index = tc.TryGetProperty("index", out var idx) ? idx.GetInt32() : pendingCalls.Count;
                            if (!pendingCalls.TryGetValue(index, out var call))
                            {
                                call = new LlmToolCall();
                                pendingCalls[index] = call;
                            }
                            if (tc.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                            {
                                call.Id = id.GetString()!;
                            }
                            if (tc.TryGetProperty("function", out var fn))
                            {
                                if (fn.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                                {
                                    call.Name += name.GetString();
                                }
                                if (fn.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String)
                                {
                                    call.ArgumentsJson += args.GetString();
                                }
                            }
                        }
                    }
                }

                if (choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String)
                {
                    yield return LlmStreamEvent.CompletedOf(finish.GetString());
                }
            }

            foreach (var call in pendingCalls.OrderBy(kv => kv.Key).Select(kv => kv.Value))
            {
                if (!string.IsNullOrEmpty(call.Name))
                {
                    yield return LlmStreamEvent.ToolCallOf(call);
                }
            }
        }
    }

    internal static object BuildBody(LlmRequest request)
    {
        var messages = new List<object>();
        foreach (var m in request.Messages)
        {
            if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
            {
                var toolCalls = m.ToolCalls.Select(c => new
                {
                    id = c.Id,
                    type = "function",
                    function = new { name = c.Name, arguments = c.ArgumentsJson },
                }).ToList();
                messages.Add(new { role = "assistant", content = m.Content, tool_calls = toolCalls });
            }
            else if (m.Role == "tool")
            {
                messages.Add(new { role = "tool", tool_call_id = m.ToolCallId, content = m.Content ?? string.Empty });
            }
            else
            {
                messages.Add(new { role = m.Role, content = m.Content ?? string.Empty });
            }
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = true,
            ["max_tokens"] = request.MaxOutputTokens,
        };

        if (request.Tools is { Count: > 0 })
        {
            body["tools"] = request.Tools.Select(t => new
            {
                type = "function",
                function = new { name = t.Name, description = t.Description, parameters = t.ParseParametersSchema() },
            }).ToList();
        }

        if (request.ReasoningEffort != Domain.Enums.ReasoningEffort.Off)
        {
            body["reasoning_effort"] = request.ReasoningEffort switch
            {
                Domain.Enums.ReasoningEffort.Low => "low",
                Domain.Enums.ReasoningEffort.Medium => "medium",
                Domain.Enums.ReasoningEffort.High => "high",
                _ => "low",
            };
        }

        return body;
    }

    internal static string CombineUrl(string baseUrl, string path)
    {
        baseUrl = baseUrl.TrimEnd('/');
        // BaseUrl 已带 /v1 结尾时避免双拼（如 https://api.example.com/v1 + /v1/chat/completions）
        if (path.StartsWith("/v1/", StringComparison.Ordinal) && baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}{path["/v1".Length..]}";
        }
        return $"{baseUrl}{path}";
    }

    private static string Truncate(string text) => text.Length > 500 ? text[..500] : text;
}
