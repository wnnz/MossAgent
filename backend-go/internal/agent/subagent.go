package agent

import (
	"fmt"

	"codingagent/internal/domain"
	"codingagent/internal/store"
)

// SubAgentRunner 子代理执行器：独立 AgentLoop，禁 task 防递归，使用子代理配置的提供商/模型。
type SubAgentRunner struct {
	Proxies *store.DB
	LLM     StreamFactory
	Loop    *Loop
}

// Run 执行子代理，返回最终文本（workspacePath 透传主会话沙箱）。
func (r *SubAgentRunner) Run(name, input, workspacePath string) (string, error) {
	agents, err := r.Proxies.ListSubAgents(true)
	if err != nil {
		return "", fmt.Errorf("读取子代理失败: %w", err)
	}
	var sub *domain.SubAgent
	for i := range agents {
		if agents[i].Name == name {
			sub = &agents[i]
			break
		}
	}
	if sub == nil {
		return "", fmt.Errorf("子代理不存在或未启用: %s", name)
	}

	allowedTools, err := parseAllowedTools(sub.AllowedToolsJSON)
	if err != nil {
		return "", fmt.Errorf("解析允许工具列表失败: %w", err)
	}
	// 禁 task 防递归；空列表视为不限制
	if allowedTools != nil {
		delete(allowedTools, "task")
		if len(allowedTools) == 0 {
			allowedTools = nil
		}
	}

	session := domain.Session{
		ID:              newSessionID(),
		ProviderID:      sub.ProviderID,
		ModelID:         sub.ModelID,
		ReasoningEffort: sub.ReasoningEffort,
		WorkspacePath:   &workspacePath,
	}

	opts := Options{
		MaxTurns:             sub.MaxTurns,
		AllowedToolNames:     allowedTools,
		SystemPromptOverride: buildSubAgentPrompt(sub),
		PersistMessages:      false,
	}

	events := make(chan domain.AgentEvent, 64)
	cancel := make(chan struct{})
	defer close(cancel)

	finalText := ""
	go r.Loop.Run(session, input, nil, opts, events, cancel)

	for evt := range events {
		switch evt.Type {
		case domain.EvtTurnCompleted:
			if data, ok := evt.Data.(map[string]any); ok {
				if content, ok := data["content"].(string); ok {
					finalText = content
				}
			}
		case domain.EvtError:
			if data, ok := evt.Data.(map[string]any); ok {
				if msg, ok := data["message"].(string); ok {
					finalText = "[子代理错误] " + msg
				}
			}
		}
	}
	return finalText, nil
}

func buildSubAgentPrompt(sub *domain.SubAgent) string {
	prompt := "你是子代理「" + sub.Name + "」。专注完成主代理交给你的任务，直接返回最终结果文本，不与用户直接交互。"
	if sub.SystemPrompt != "" {
		prompt += "\n" + sub.SystemPrompt
	}
	return prompt
}

func parseAllowedTools(jsonStr string) (map[string]bool, error) {
	var list []string
	if err := jsonUnmarshalList(jsonStr, &list); err != nil {
		return nil, err
	}
	if len(list) == 0 {
		return nil, nil
	}
	m := map[string]bool{}
	for _, t := range list {
		m[t] = true
	}
	return m, nil
}
