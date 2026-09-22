package domain

import "encoding/json"

// LLM 请求/消息/工具调用模型。
type LlmMessage struct {
	Role      string       `json:"role"`
	Content   *string      `json:"content,omitempty"`
	ToolCalls []LlmToolCall `json:"toolCalls,omitempty"`
	ToolCallID *string     `json:"toolCallId,omitempty"`
	Name      *string      `json:"name,omitempty"`
}

type LlmToolCall struct {
	ID            string `json:"id"`
	Name          string `json:"name"`
	ArgumentsJSON string `json:"argumentsJson"`
}

// ParseArguments 安全解析参数为 map，畸形 JSON 回退空对象（不抛错）。
func (c LlmToolCall) ParseArguments() map[string]any {
	var m map[string]any
	if err := json.Unmarshal([]byte(c.ArgumentsJSON), &m); err != nil || m == nil {
		return map[string]any{}
	}
	return m
}

type LlmToolDefinition struct {
	Name        string `json:"name"`
	Description string `json:"description"`
	SchemaJSON  string `json:"schemaJson"`
}

type LlmRequest struct {
	Model           string            `json:"model"`
	Messages        []LlmMessage      `json:"messages"`
	Tools           []LlmToolDefinition `json:"tools,omitempty"`
	ReasoningEffort ReasoningEffort   `json:"reasoningEffort"`
	MaxOutputTokens int               `json:"maxOutputTokens"`
}

type LlmStreamEventType int

const (
	LlmDelta LlmStreamEventType = iota
	LlmToolCallEvt
	LlmCompleted
	LlmError
)

type LlmStreamEvent struct {
	Type         LlmStreamEventType
	Delta        string
	ToolCall     *LlmToolCall
	FinishReason string
	Error        string
}

// Agent 循环事件（SSE 负载）。
type AgentEventType int

const (
	EvtTurnStarted AgentEventType = iota
	EvtMessageDelta
	EvtToolCallStarted
	EvtToolCallFinished
	EvtTurnCompleted
	EvtCancelled
	EvtError
)

type AgentEvent struct {
	Type AgentEventType
	Data any
}

// ToolContext 工具执行上下文。
type ToolContext struct {
	SessionID        string
	WorkspacePath    string
	AllowedToolNames map[string]bool // nil 表示不限制
}

// ToolResult 工具执行结果。
type ToolResult struct {
	Success bool
	Output  string
}

// Tool 工具接口。
type Tool interface {
	Name() string
	Description() string
	ParametersSchemaJSON() string
	Execute(argumentsJSON string, ctx ToolContext) ToolResult
}

// McpToolInfo tools/list 结果。
type McpToolInfo struct {
	Name        string `json:"name"`
	Description string `json:"description"`
	SchemaJSON  string `json:"schemaJson"`
}

// McpConnection MCP 连接（initialize → tools/list → tools/call）。
type McpConnection interface {
	ServerID() int64
	ServerName() string
	IsConnected() bool
	Initialize() error
	ListTools() ([]McpToolInfo, error)
	CallTool(toolName, argumentsJSON string) (string, error)
	Close()
}
