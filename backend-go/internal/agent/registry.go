package agent

import (
	"encoding/json"
	"fmt"

	"codingagent/internal/domain"
	"codingagent/internal/mcp"
	"codingagent/internal/store"
	"codingagent/internal/tools"
)

// Registry 工具注册表（静态内置 + 已连接 MCP 桥接 + task 执行器）。
type Registry struct {
	Proxies *store.DB
	Bridge  *mcp.Bridge
	// SubAgentNames 子代理名称 → 执行函数（由 Loop 注入，避免构造期循环）
	TaskRunner func(subagent, input, workspacePath string) string
}

// All 全部可用工具。
func (r *Registry) All() []domain.Tool {
	adapter := PromptStoreAdapter{Proxies: r.Proxies}
	var out []domain.Tool
	out = append(out,
		tools.ReadFile{},
		tools.WriteFile{},
		tools.EditFile{},
		tools.ListDir{},
		tools.Glob{},
		tools.Grep{},
		tools.RunCommand{},
		tools.MemorySave{Store: adapter},
		tools.MemorySearch{Store: adapter},
		tools.SkillLoad{Store: adapter},
	)
	if r.TaskRunner != nil {
		out = append(out, TaskTool{Runner: r.TaskRunner})
	}
	out = append(out, r.Bridge.ConnectedTools()...)
	return out
}

// Definitions LLM 工具定义。
func (r *Registry) Definitions() []domain.LlmToolDefinition {
	var out []domain.LlmToolDefinition
	for _, t := range r.All() {
		out = append(out, domain.LlmToolDefinition{Name: t.Name(), Description: t.Description(), SchemaJSON: t.ParametersSchemaJSON()})
	}
	return out
}

// McpToolNames 已连接 MCP 工具名（系统提示词用）。
func (r *Registry) McpToolNames() []string {
	var names []string
	for _, t := range r.Bridge.ConnectedTools() {
		names = append(names, t.Name())
	}
	return names
}

// ---- store 适配器（memory/skill 工具 + 系统提示词） ----

type PromptStoreAdapter struct {
	Proxies *store.DB
}

func (a PromptStoreAdapter) ListMemoriesScope(scope string) ([]MemorySummary, error) {
	items, err := a.Proxies.ListMemories(scope)
	if err != nil {
		return nil, err
	}
	out := make([]MemorySummary, len(items))
	for i, m := range items {
		out[i] = MemorySummary{Scope: string(m.Scope), Title: m.Title, Content: m.Content}
	}
	return out, nil
}

func (a PromptStoreAdapter) ListSkillsEnabled() ([]SkillSummary, error) {
	items, err := a.Proxies.ListSkills(true)
	if err != nil {
		return nil, err
	}
	out := make([]SkillSummary, len(items))
	for i, s := range items {
		out[i] = SkillSummary{Name: s.Name, Description: s.Description}
	}
	return out, nil
}

func (a PromptStoreAdapter) ListSubAgentsEnabled() ([]SubAgentSummary, error) {
	items, err := a.Proxies.ListSubAgents(true)
	if err != nil {
		return nil, err
	}
	out := make([]SubAgentSummary, len(items))
	for i, s := range items {
		out[i] = SubAgentSummary{Name: s.Name, Description: s.Description, InvocationRule: s.InvocationRule}
	}
	return out, nil
}

// AddMemory 实现 tools.MemoryStore。
func (a PromptStoreAdapter) AddMemory(scope, title, content, tags string) error {
	var tagPtr *string
	if tags != "" {
		tagPtr = &tags
	}
	item := &domain.Memory{Scope: domain.MemoryScope(scope), Title: title, Content: content, Tags: tagPtr, UpdatedAt: nowRFC3339()}
	return a.Proxies.AddMemory(item)
}

// SearchMemories 实现 tools.MemoryStore。
func (a PromptStoreAdapter) SearchMemories(q string) ([]tools.MemoryHit, error) {
	items, err := a.Proxies.SearchMemories(q)
	if err != nil {
		return nil, err
	}
	out := make([]tools.MemoryHit, len(items))
	for i, m := range items {
		tags := ""
		if m.Tags != nil {
			tags = *m.Tags
		}
		out[i] = tools.MemoryHit{Scope: string(m.Scope), Title: m.Title, Content: m.Content, Tags: tags}
	}
	return out, nil
}

// LoadSkill 实现 tools.SkillLoader。
func (a PromptStoreAdapter) LoadSkill(name string) (string, string, error) {
	skill, err := a.Proxies.GetSkillByName(name)
	if err != nil || !skill.Enabled {
		return "", "", fmt.Errorf("技能不存在或未启用")
	}
	return skill.Name, skill.Instructions, nil
}

// ---- task 工具（子代理执行器注入） ----

// TaskTool 带子代理执行器的 task 工具（沙箱透传主会话工作区）。
type TaskTool struct {
	Runner func(subagent, input, workspacePath string) string
}

func (TaskTool) Name() string { return "task" }
func (TaskTool) Description() string {
	return "调用子代理完成独立任务。参数: {\"subagent\": \"子代理名称\", \"input\": \"任务输入\"}"
}
func (TaskTool) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"subagent":{"type":"string","description":"子代理名称"},"input":{"type":"string","description":"任务输入"}},"required":["subagent","input"]}`
}
func (t TaskTool) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	var req struct {
		Subagent string `json:"subagent"`
		Input    string `json:"input"`
	}
	if err := json.Unmarshal([]byte(argsJSON), &req); err != nil || req.Subagent == "" || req.Input == "" {
		return domain.ToolResult{Success: false, Output: "subagent 和 input 不能为空"}
	}
	if ctx.WorkspacePath == "" {
		return domain.ToolResult{Success: false, Output: "子代理缺少工作区，拒绝执行"}
	}
	return domain.ToolResult{Success: true, Output: t.Runner(req.Subagent, req.Input, ctx.WorkspacePath)}
}
