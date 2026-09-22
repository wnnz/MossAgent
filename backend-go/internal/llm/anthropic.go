package llm

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strings"

	"codingagent/internal/domain"
)

// Anthropic 协议客户端（/v1/messages SSE 流式；system 顶格 + tool_use/tool_result 块）。
type AnthropicClient struct {
	HTTP    *http.Client
	BaseURL string
	APIKey  string
}

func (c *AnthropicClient) endpoint(path string) string {
	base := strings.TrimRight(c.BaseURL, "/")
	if strings.HasPrefix(path, "/v1/") && strings.HasSuffix(strings.ToLower(base), "/v1") {
		return base + path[len("/v1"):]
	}
	return base + path
}

func (c *AnthropicClient) Stream(ctx context.Context, req domain.LlmRequest, out chan<- domain.LlmStreamEvent) {
	defer close(out)

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

	body := buildAnthropicBody(req)
	payload, err := json.Marshal(body)
	if err != nil {
		send(errorEvent("构建请求失败: " + err.Error()))
		return
	}

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, c.endpoint("/v1/messages"), strings.NewReader(string(payload)))
	if err != nil {
		send(errorEvent("构建请求失败: " + err.Error()))
		return
	}
	httpReq.Header.Set("Content-Type", "application/json")
	httpReq.Header.Set("x-api-key", c.APIKey)
	httpReq.Header.Set("anthropic-version", "2023-06-01")

	resp, err := c.HTTP.Do(httpReq)
	if err != nil {
		if ctx.Err() != nil {
			return
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

	currentID := ""
	currentName := ""
	currentArgs := ""

	flushToolCall := func() bool {
		if currentID != "" && currentName != "" {
			if !send(domain.LlmStreamEvent{
				Type: domain.LlmToolCallEvt,
				ToolCall: &domain.LlmToolCall{
					ID:            currentID,
					Name:          currentName,
					ArgumentsJSON: currentArgs,
				},
			}) {
				return false
			}
		}
		currentID = ""
		currentArgs = ""
		return true
	}

	err = pumpSSE(resp, func(data string) bool {
		if data == "" {
			return true
		}
		var evt struct {
			Type string `json:"type"`
			ContentBlock *struct {
				Type string `json:"type"`
				ID   string `json:"id"`
				Name string `json:"name"`
			} `json:"content_block"`
			Delta *struct {
				Type        string `json:"type"`
				Text        string `json:"text"`
				PartialJSON string `json:"partial_json"`
				StopReason  string `json:"stop_reason"`
			} `json:"delta"`
			Error *struct {
				Message string `json:"message"`
			} `json:"error"`
		}
		if !decodeJSON(data, &evt) {
			return true
		}

		switch evt.Type {
		case "content_block_start":
			if evt.ContentBlock != nil && evt.ContentBlock.Type == "tool_use" {
				// 冲刷上一个工具调用
				if !flushToolCall() {
					return false
				}
				currentID = evt.ContentBlock.ID
				currentName = evt.ContentBlock.Name
			}
		case "content_block_delta":
			if evt.Delta == nil {
				return true
			}
			switch evt.Delta.Type {
			case "text_delta":
				if !send(domain.LlmStreamEvent{Type: domain.LlmDelta, Delta: evt.Delta.Text}) {
					return false
				}
			case "input_json_delta":
				currentArgs += evt.Delta.PartialJSON
			}
		case "content_block_stop":
			if !flushToolCall() {
				return false
			}
		case "message_delta":
			if evt.Delta != nil && evt.Delta.StopReason != "" {
				if !send(domain.LlmStreamEvent{Type: domain.LlmCompleted, FinishReason: evt.Delta.StopReason}) {
					return false
				}
			}
		case "error":
			msg := "未知错误"
			if evt.Error != nil && evt.Error.Message != "" {
				msg = evt.Error.Message
			}
			send(errorEvent(msg))
			return false
		}
		return true
	})
	_ = err
}

// buildAnthropicBody 构造 /v1/messages 请求体（system 顶格，tool_result 合并为 user 块）。
func buildAnthropicBody(req domain.LlmRequest) map[string]any {
	system := ""
	messages := []map[string]any{}
	var pendingToolResults []map[string]any

	flushToolResults := func() {
		if len(pendingToolResults) > 0 {
			messages = append(messages, map[string]any{"role": "user", "content": pendingToolResults})
			pendingToolResults = nil
		}
	}

	for _, m := range req.Messages {
		switch {
		case m.Role == "system":
			system += derefOr(m.Content, "")
		case m.Role == "assistant" && len(m.ToolCalls) > 0:
			flushToolResults()
			blocks := []map[string]any{}
			if m.Content != nil && *m.Content != "" {
				blocks = append(blocks, map[string]any{"type": "text", "text": *m.Content})
			}
			for _, c := range m.ToolCalls {
				var input map[string]any
				if err := json.Unmarshal([]byte(orEmpty(c.ArgumentsJSON, "{}")), &input); err != nil || input == nil {
					input = map[string]any{}
				}
				blocks = append(blocks, map[string]any{
					"type":  "tool_use",
					"id":    c.ID,
					"name":  c.Name,
					"input": input,
				})
			}
			messages = append(messages, map[string]any{"role": "assistant", "content": blocks})
		case m.Role == "tool":
			pendingToolResults = append(pendingToolResults, map[string]any{
				"type":        "tool_result",
				"tool_use_id": derefOr(m.ToolCallID, ""),
				"content":     derefOr(m.Content, ""),
			})
		default:
			flushToolResults()
			messages = append(messages, map[string]any{"role": m.Role, "content": derefOr(m.Content, "")})
		}
	}
	flushToolResults()

	maxTokens := req.MaxOutputTokens
	body := map[string]any{
		"model":      req.Model,
		"max_tokens": maxTokens,
		"messages":   messages,
		"stream":     true,
	}
	if system != "" {
		body["system"] = system
	}

	if len(req.Tools) > 0 {
		tools := []map[string]any{}
		for _, t := range req.Tools {
			var schema map[string]any
			if err := json.Unmarshal([]byte(orEmpty(t.SchemaJSON, "{}")), &schema); err != nil || schema == nil {
				schema = map[string]any{}
			}
			tools = append(tools, map[string]any{
				"name":         t.Name,
				"description":  t.Description,
				"input_schema": schema,
			})
		}
		body["tools"] = tools
	}

	// Anthropic 扩展思考：budget 必须 < max_tokens
	if req.ReasoningEffort != domain.EffortOff && req.ReasoningEffort != "" {
		budget := 2048
		switch req.ReasoningEffort {
		case domain.EffortMedium:
			budget = 8192
		case domain.EffortHigh:
			budget = 16384
		}
		if budget >= maxTokens {
			body["max_tokens"] = budget + 4096
		}
		body["thinking"] = map[string]any{"type": "enabled", "budget_tokens": budget}
	}

	return body
}
