package llm

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strings"

	"codingagent/internal/domain"
)

// OpenAI 兼容协议客户端（/v1/chat/completions SSE 流式）。
type OpenAIClient struct {
	HTTP    *http.Client
	BaseURL string
	APIKey  string
}

func (c *OpenAIClient) endpoint(path string) string {
	base := strings.TrimRight(c.BaseURL, "/")
	// BaseUrl 已带 /v1 结尾时避免双拼
	if strings.HasPrefix(path, "/v1/") && strings.HasSuffix(strings.ToLower(base), "/v1") {
		return base + path[len("/v1"):]
	}
	return base + path
}

func (c *OpenAIClient) Stream(ctx context.Context, req domain.LlmRequest, out chan<- domain.LlmStreamEvent) {
	defer close(out)

	// 增量发送：ctx 取消时中止（避免提前返回后 goroutine/连接泄漏）
	send := func(e domain.LlmStreamEvent) bool {
		select {
		case out <- e:
			return true
		case <-ctx.Done():
			return false
		}
	}
	errorEvent := func(msg string) domain.LlmStreamEvent {
		return domain.LlmStreamEvent{Type: domain.LlmError, Error: msg}
	}

	body := buildOpenAIBody(req)
	payload, err := json.Marshal(body)
	if err != nil {
		send(errorEvent("构建请求失败: " + err.Error()))
		return
	}

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, c.endpoint("/v1/chat/completions"), strings.NewReader(string(payload)))
	if err != nil {
		send(errorEvent("构建请求失败: " + err.Error()))
		return
	}
	httpReq.Header.Set("Content-Type", "application/json")
	if c.APIKey != "" {
		httpReq.Header.Set("Authorization", "Bearer "+c.APIKey)
	}

	resp, err := c.HTTP.Do(httpReq)
	if err != nil {
		if ctx.Err() != nil {
			return // 调用方取消，静默
		}
		send(errorEvent("LLM 请求失败: " + err.Error()))
		return
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		buf := make([]byte, 500)
		n, _ := resp.Body.Read(buf)
		send(errorEvent(fmt.Sprintf("LLM 返回 %d: %s", resp.StatusCode, string(buf[:n]))))
		return
	}

	// tool_calls 分片按 index 聚合（id/name/arguments 拼接）
	type pending struct {
		id, name, args string
	}
	calls := map[int]*pending{}
	order := []int{}

	err = pumpSSE(resp, func(data string) bool {
		if data == "" {
			return true
		}
		if data == "[DONE]" {
			return false
		}
		var chunk struct {
			Choices []struct {
				Delta struct {
					Content   string `json:"content"`
					ToolCalls []struct {
						Index    *int   `json:"index"`
						ID       string `json:"id"`
						Function struct {
							Name      string `json:"name"`
							Arguments string `json:"arguments"`
						} `json:"function"`
					} `json:"tool_calls"`
				} `json:"delta"`
				FinishReason *string `json:"finish_reason"`
			} `json:"choices"`
		}
		if !decodeJSON(data, &chunk) {
			return true
		}
		for _, choice := range chunk.Choices {
			for _, tc := range choice.Delta.ToolCalls {
				idx := 0
				if tc.Index != nil {
					idx = *tc.Index
				} else {
					idx = len(calls) // 无 index 时按出现序
				}
				p, ok := calls[idx]
				if !ok {
					p = &pending{}
					calls[idx] = p
					order = append(order, idx)
				}
				if tc.ID != "" {
					p.id = tc.ID
				}
				if tc.Function.Name != "" {
					p.name += tc.Function.Name
				}
				if tc.Function.Arguments != "" {
					p.args += tc.Function.Arguments
				}
			}
			if choice.Delta.Content != "" {
				if !send(domain.LlmStreamEvent{Type: domain.LlmDelta, Delta: choice.Delta.Content}) {
					return false
				}
			}
			if choice.FinishReason != nil {
				if !send(domain.LlmStreamEvent{Type: domain.LlmCompleted, FinishReason: *choice.FinishReason}) {
					return false
				}
			}
		}
		return true
	})
	// 流中止（含 [DONE]）：ctx 已取消或正常结束，均不再产出
	if err != nil && ctx.Err() != nil {
		return
	}

	// 按 index 顺序产出工具调用
	for _, idx := range order {
		p := calls[idx]
		if p != nil && p.name != "" {
			if !send(domain.LlmStreamEvent{
				Type: domain.LlmToolCallEvt,
				ToolCall: &domain.LlmToolCall{
					ID:            p.id,
					Name:          p.name,
					ArgumentsJSON: p.args,
				},
			}) {
				return
			}
		}
	}
}

// buildOpenAIBody 构造 /v1/chat/completions 请求体（与 C# 版契约一致）。
func buildOpenAIBody(req domain.LlmRequest) map[string]any {
	messages := []map[string]any{}
	for _, m := range req.Messages {
		switch {
		case m.Role == "assistant" && len(m.ToolCalls) > 0:
			toolCalls := []map[string]any{}
			for _, c := range m.ToolCalls {
				toolCalls = append(toolCalls, map[string]any{
					"id":   c.ID,
					"type": "function",
					"function": map[string]any{
						"name":      c.Name,
						"arguments": c.ArgumentsJSON,
					},
				})
			}
			msg := map[string]any{"role": "assistant", "tool_calls": toolCalls}
			if m.Content != nil {
				msg["content"] = *m.Content
			}
			messages = append(messages, msg)
		case m.Role == "tool":
			content := ""
			if m.Content != nil {
				content = *m.Content
			}
			messages = append(messages, map[string]any{
				"role":         "tool",
				"tool_call_id": derefOr(m.ToolCallID, ""),
				"content":      content,
			})
		default:
			content := ""
			if m.Content != nil {
				content = *m.Content
			}
			messages = append(messages, map[string]any{"role": m.Role, "content": content})
		}
	}

	body := map[string]any{
		"model":      req.Model,
		"messages":   messages,
		"stream":     true,
		"max_tokens": req.MaxOutputTokens,
	}

	if len(req.Tools) > 0 {
		tools := []map[string]any{}
		for _, t := range req.Tools {
			var schema map[string]any
			if err := json.Unmarshal([]byte(orEmpty(t.SchemaJSON, "{}")), &schema); err != nil {
				schema = map[string]any{}
			}
			tools = append(tools, map[string]any{
				"type": "function",
				"function": map[string]any{
					"name":        t.Name,
					"description": t.Description,
					"parameters":  schema,
				},
			})
		}
		body["tools"] = tools
	}

	if req.ReasoningEffort != domain.EffortOff && req.ReasoningEffort != "" {
		body["reasoning_effort"] = string(req.ReasoningEffort)
	}

	return body
}

func derefOr(p *string, fallback string) string {
	if p == nil {
		return fallback
	}
	return *p
}

func orEmpty(s, fallback string) string {
	if s == "" {
		return fallback
	}
	return s
}
