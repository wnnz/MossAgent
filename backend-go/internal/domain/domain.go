// Package domain：领域实体与字符串枚举（与前端 camelCase + lower_snake_case 契约一致）。
package domain

// 枚举统一用字符串类型，序列化/存储零转换。
type (
	ProviderType     string
	ProxyScheme      string
	MessageRole      string
	McpTransport     string
	ReasoningEffort  string
	MemoryScope      string
	SessionStatus    string
)

const (
	ProviderOpenAiCompatible ProviderType = "openai_compatible"
	ProviderAnthropic        ProviderType = "anthropic"

	SchemeHTTP   ProxyScheme = "http"
	SchemeSocks5 ProxyScheme = "socks5"

	RoleUser      MessageRole = "user"
	RoleAssistant MessageRole = "assistant"
	RoleTool      MessageRole = "tool"
	RoleSystem    MessageRole = "system"

	TransportStdio McpTransport = "stdio"
	TransportHTTP  McpTransport = "http"

	EffortOff    ReasoningEffort = "off"
	EffortLow    ReasoningEffort = "low"
	EffortMedium ReasoningEffort = "medium"
	EffortHigh   ReasoningEffort = "high"

	ScopeGlobal  MemoryScope = "global"
	ScopeProject MemoryScope = "project"

	StatusActive   SessionStatus = "active"
	StatusArchived SessionStatus = "archived"
)

// ParseEffort 宽松解析思考强度，非法值回退 off。
func ParseEffort(s string) ReasoningEffort {
	switch ReasoningEffort(s) {
	case EffortLow, EffortMedium, EffortHigh, EffortOff:
		return ReasoningEffort(s)
	}
	return EffortOff
}

// ---- 实体 ----

type AuthRecord struct {
	ID           int64
	PasswordHash string
	Salt         string
	CreatedAt    string // RFC3339
}

type Provider struct {
	ID       int64           `json:"id"`
	Name     string          `json:"name"`
	Type     ProviderType    `json:"type"`
	BaseURL  string          `json:"baseUrl"`
	APIKey   string          `json:"apiKey"`
	ProxyID  *int64          `json:"proxyId"`
	Enabled  bool            `json:"enabled"`
	Models   []ProviderModel `json:"models"`
}

type ProviderModel struct {
	ID                    int64           `json:"id"`
	ProviderID            int64           `json:"providerId"`
	ModelID               string          `json:"modelId"`
	DisplayName           string          `json:"displayName"`
	SupportsTools         bool            `json:"supportsTools"`
	SupportsReasoning     bool            `json:"supportsReasoning"`
	DefaultReasoningEffort ReasoningEffort `json:"defaultReasoningEffort"`
	MaxContextTokens      int             `json:"maxContextTokens"`
	MaxOutputTokens       int             `json:"maxOutputTokens"`
	IsCustom              bool            `json:"isCustom"`
}

type Proxy struct {
	ID       int64       `json:"id"`
	Name     string      `json:"name"`
	Scheme   ProxyScheme `json:"scheme"`
	Host     string      `json:"host"`
	Port     int         `json:"port"`
	Username *string     `json:"username"`
	Password *string     `json:"password"`
	Enabled  bool        `json:"enabled"`
}

type Session struct {
	ID              string          `json:"id"`
	Title           string          `json:"title"`
	ProjectID       *int64          `json:"projectId"`
	ProviderID      int64           `json:"providerId"`
	ModelID         string          `json:"modelId"`
	ReasoningEffort ReasoningEffort `json:"reasoningEffort"`
	WorkspacePath   *string         `json:"workspacePath"`
	Status          SessionStatus   `json:"status"`
	CreatedAt       string          `json:"createdAt"`
	UpdatedAt       string          `json:"updatedAt"`
}

type ChatMessage struct {
	ID            int64       `json:"id"`
	SessionID     string      `json:"sessionId"`
	Role          MessageRole `json:"role"`
	Content       string      `json:"content"`
	ToolCallsJSON *string     `json:"toolCallsJson"`
	ToolCallID    *string     `json:"toolCallId"`
	Name          *string     `json:"name"`
	CreatedAt     string      `json:"createdAt"`
}

type SubAgent struct {
	ID               int64           `json:"id"`
	Name             string          `json:"name"`
	Description      string          `json:"description"`
	InvocationRule   string          `json:"invocationRule"`
	SystemPrompt     string          `json:"systemPrompt"`
	ProviderID       int64           `json:"providerId"`
	ModelID          string          `json:"modelId"`
	ReasoningEffort  ReasoningEffort `json:"reasoningEffort"`
	MaxTurns         int             `json:"maxTurns"`
	AllowedToolsJSON string          `json:"allowedToolsJson"`
	Enabled          bool            `json:"enabled"`
}

type Memory struct {
	ID        int64       `json:"id"`
	Scope     MemoryScope `json:"scope"`
	Title     string      `json:"title"`
	Content   string      `json:"content"`
	Tags      *string     `json:"tags"`
	UpdatedAt string      `json:"updatedAt"`
}

type Skill struct {
	ID           int64  `json:"id"`
	Name         string `json:"name"`
	Description  string `json:"description"`
	Instructions string `json:"instructions"`
	Enabled      bool   `json:"enabled"`
}

type McpServer struct {
	ID          int64        `json:"id"`
	Name        string       `json:"name"`
	Transport   McpTransport `json:"transport"`
	Command     *string      `json:"command"`
	ArgsJSON    *string      `json:"argsJson"`
	EnvJSON     *string      `json:"envJson"`
	URL         *string      `json:"url"`
	HeadersJSON *string      `json:"headersJson"`
	Enabled     bool         `json:"enabled"`
	AutoConnect bool         `json:"autoConnect"`
}

type McpTool struct {
	ID        int64  `json:"id"`
	ServerID  int64  `json:"serverId"`
	Name      string `json:"name"`
	Description string `json:"description"`
	SchemaJSON string `json:"schemaJson"`
}

type Setting struct {
	Key   string `json:"key"`
	Value string `json:"value"`
}

type Project struct {
	ID        int64  `json:"id"`
	Name      string `json:"name"`
	Path      string `json:"path"`
	CreatedAt string `json:"createdAt"`
}
