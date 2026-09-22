package llm

import (
	"strings"
	"testing"
	"time"

	"codingagent/internal/domain"
	"codingagent/internal/store"
)

// ---- 请求体转换（与 C# 版契约一致） ----

func TestBuildOpenAIBody_ToolMessage_OutputsToolCallID(t *testing.T) {
	content := "result-text"
	req := domain.LlmRequest{
		Model: "m",
		Messages: []domain.LlmMessage{
			{Role: "system", Content: strPtr("sys")},
			{Role: "user", Content: strPtr("hi")},
			{Role: "assistant", ToolCalls: []domain.LlmToolCall{{ID: "call-1", Name: "echo", ArgumentsJSON: `{"text":"x"}`}}},
			{Role: "tool", Content: &content, ToolCallID: strPtr("call-1"), Name: strPtr("echo")},
		},
		Tools: []domain.LlmToolDefinition{{Name: "echo", Description: "回显", SchemaJSON: `{"type":"object"}`}},
	}

	body := buildOpenAIBody(req)
	messages := body["messages"].([]map[string]any)

	if len(messages) != 4 {
		t.Fatalf("应有 4 条消息，实际 %d", len(messages))
	}
	toolMsg := messages[3]
	if toolMsg["role"] != "tool" {
		t.Fatalf("第 4 条应为 tool，实际 %v", toolMsg["role"])
	}
	if toolMsg["tool_call_id"] != "call-1" {
		t.Fatalf("tool_call_id 应为 call-1，实际 %v", toolMsg["tool_call_id"])
	}
	assistant := messages[2]
	toolCalls := assistant["tool_calls"].([]map[string]any)
	if toolCalls[0]["id"] != "call-1" {
		t.Fatalf("assistant tool_calls 应保留 id")
	}
	fn := toolCalls[0]["function"].(map[string]any)
	if fn["name"] != "echo" || fn["arguments"] != `{"text":"x"}` {
		t.Fatalf("function 转换错误: %v", fn)
	}
	if _, ok := body["reasoning_effort"]; ok {
		t.Fatal("思考强度 off 时不应出现 reasoning_effort")
	}
}

func TestBuildOpenAIBody_ReasoningMedium(t *testing.T) {
	req := domain.LlmRequest{Model: "m", ReasoningEffort: domain.EffortMedium}
	body := buildOpenAIBody(req)
	if body["reasoning_effort"] != "medium" {
		t.Fatalf("reasoning_effort 应为 medium，实际 %v", body["reasoning_effort"])
	}
}

func TestBuildAnthropicBody_SystemLifted_ToolResultsMerged(t *testing.T) {
	r1, r2 := "r1", "r2"
	req := domain.LlmRequest{
		Model: "m", MaxOutputTokens: 8192,
		Messages: []domain.LlmMessage{
			{Role: "system", Content: strPtr("sys prompt")},
			{Role: "user", Content: strPtr("hi")},
			{Role: "assistant", ToolCalls: []domain.LlmToolCall{{ID: "call-1", Name: "echo", ArgumentsJSON: `{"text":"x"}`}}},
			{Role: "tool", Content: &r1, ToolCallID: strPtr("call-1")},
			{Role: "tool", Content: &r2, ToolCallID: strPtr("call-2")},
			{Role: "user", Content: strPtr("thanks")},
		},
	}

	body := buildAnthropicBody(req)
	if body["system"] != "sys prompt" {
		t.Fatalf("system 应顶格，实际 %v", body["system"])
	}
	messages := body["messages"].([]map[string]any)
	if len(messages) != 4 {
		t.Fatalf("连续 tool 应合并为 1 个 user 块：应有 4 条消息，实际 %d", len(messages))
	}
	results := messages[2]["content"].([]map[string]any)
	if len(results) != 2 || results[0]["type"] != "tool_result" || results[0]["tool_use_id"] != "call-1" {
		t.Fatalf("tool_result 合并错误: %v", results)
	}
	blocks := messages[1]["content"].([]map[string]any)
	if blocks[0]["type"] != "tool_use" || blocks[0]["id"] != "call-1" {
		t.Fatalf("assistant tool_use 转换错误: %v", blocks)
	}
}

func TestBuildAnthropicBody_ThinkingBudgetAutoRaises(t *testing.T) {
	req := domain.LlmRequest{Model: "m", MaxOutputTokens: 8192, ReasoningEffort: domain.EffortMedium}
	body := buildAnthropicBody(req)
	if body["max_tokens"] != 8192+4096 {
		t.Fatalf("budget >= max_tokens 时应抬高 max_tokens，实际 %v", body["max_tokens"])
	}
}

// ---- URL 归一 ----

func TestOpenAIEndpoint_AvoidsV1DoubleJoin(t *testing.T) {
	c := &OpenAIClient{BaseURL: "https://api.example.com/v1"}
	if got := c.endpoint("/v1/chat/completions"); got != "https://api.example.com/v1/chat/completions" {
		t.Fatalf("不应双拼 /v1: %s", got)
	}
	c2 := &OpenAIClient{BaseURL: "https://api.example.com"}
	if got := c2.endpoint("/v1/chat/completions"); got != "https://api.example.com/v1/chat/completions" {
		t.Fatalf("应拼接 /v1: %s", got)
	}
}

// ---- 工厂与缓存 ----

func TestFactory_ClientCacheSameProvider(t *testing.T) {
	db, err := store.Open(storePath())
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	if err := db.SeedDefault("test-secret"); err != nil {
		t.Fatal(err)
	}

	f := NewFactory(db)
	p := &domain.Provider{ID: 1, Type: domain.ProviderOpenAiCompatible, BaseURL: "http://localhost"}
	fn1, err := f.Create(p)
	if err != nil {
		t.Fatal(err)
	}
	fn2, _ := f.Create(p)
	if fn1 == nil || fn2 == nil {
		t.Fatal("流式函数不应为 nil")
	}
	if f.httpCacheSize() != 1 {
		t.Fatalf("同 provider 应复用 1 个 HTTP 客户端，实际 %d", f.httpCacheSize())
	}
}

func TestStore_EmptyListReturnsEmptySlice(t *testing.T) {
	db, err := store.Open(storePath())
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	if err := db.SeedDefault("test-secret"); err != nil {
		t.Fatal(err)
	}

	providers, _ := db.ListProviders()
	if providers == nil {
		t.Fatal("空列表应返回 [] 而不是 null")
	}
	sessions, _ := db.ListSessions(false)
	if sessions == nil {
		t.Fatal("空列表应返回 [] 而不是 null")
	}
	skills, _ := db.ListSkills(false)
	if skills == nil {
		t.Fatal("空列表应返回 [] 而不是 null")
	}
}

func storePath() string {
	return strings.Join([]string{testingTempDir(), "codingagent-" + time.Now().Format("150405.000000000") + ".db"}, string(timeSeparator()))
}

func testingTempDir() string { return osTempDir() }
func timeSeparator() rune    { return '/' }
