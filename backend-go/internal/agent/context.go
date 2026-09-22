// Package agent：Agent 循环、系统提示词构建、上下文窗口管理、子代理执行器。
package agent

import (
	"fmt"
	"strings"
	"unicode/utf8"

	"codingagent/internal/domain"
)

// ContextWindowManager：按 maxContextTokens 估算 token，滑动裁剪旧消息、超长工具输出截断。
type ContextWindowManager struct{}

const charsPerToken = 4

// EstimateTokens 估算 token（chars/4）。
func (ContextWindowManager) EstimateTokens(text string) int {
	return (utf8.RuneCountInString(text) + charsPerToken - 1) / charsPerToken
}

func estimateMessage(m domain.LlmMessage) int {
	cost := 8
	if m.Content != nil {
		cost += (utf8.RuneCountInString(*m.Content) + charsPerToken - 1) / charsPerToken
	}
	for _, c := range m.ToolCalls {
		cost += (utf8.RuneCountInString(c.Name+c.ArgumentsJSON) + charsPerToken - 1) / charsPerToken
	}
	return cost
}

// Trim 裁剪消息：保留 system 与最近消息；超长工具输出截断。
func (ContextWindowManager) Trim(messages []domain.LlmMessage, maxContextTokens int) []domain.LlmMessage {
	result := make([]domain.LlmMessage, len(messages))
	for i, m := range messages {
		result[i] = truncateToolOutput(m)
	}

	total := 0
	for _, m := range result {
		total += estimateMessage(m)
	}
	if total <= maxContextTokens {
		return result
	}

	// 保留 system + 后缀消息（保 tool 配对完整性：整段 assistant+tool 回退）
	var system, rest []domain.LlmMessage
	for _, m := range result {
		if m.Role == "system" {
			system = append(system, m)
		} else {
			rest = append(rest, m)
		}
	}

	budget := maxContextTokens - 2000
	if budget < 1000 {
		budget = 1000
	}

	// 从尾部向前收（保 tool 配对完整性：整段 assistant+tool 成对保留）
	kept := []domain.LlmMessage{}
	sum := 0
	for _, m := range system {
		sum += estimateMessage(m)
	}
	i := len(rest) - 1
	for i >= 0 {
		cost := estimateMessage(rest[i])
		if sum+cost > budget && len(kept) > 8 {
			break
		}
		sum += cost
		kept = append([]domain.LlmMessage{rest[i]}, kept...)
		// tool 消息与其配对的 assistant 一并保留（成对回退）
		if rest[i].Role == "tool" && i > 0 && rest[i-1].Role == "assistant" {
			i--
			cost2 := estimateMessage(rest[i])
			sum += cost2
			kept = append([]domain.LlmMessage{rest[i]}, kept...)
		}
		i--
	}

	trimmed := append([]domain.LlmMessage{}, system...)
	if len(kept) < len(rest) {
		trimmed = append(trimmed, domain.LlmMessage{Role: "system", Content: strPtr(fmt.Sprintf("[更早的 %d 条消息已因上下文窗口限制被裁剪]", len(rest)-len(kept)))})
	}
	trimmed = append(trimmed, kept...)
	return trimmed
}

func truncateToolOutput(m domain.LlmMessage) domain.LlmMessage {
	const maxToolOutput = 8_000
	if m.Role != "tool" || m.Content == nil || len(*m.Content) <= maxToolOutput {
		return m
	}
	truncated := (*m.Content)[:maxToolOutput] + fmt.Sprintf("\n[工具输出过长已截断，原始长度 %d 字符]", len(*m.Content))
	return domain.LlmMessage{Role: "tool", Content: &truncated, ToolCallID: m.ToolCallID, Name: m.Name}
}

func strPtr(s string) *string { return &s }

// BuildRequestMessages 组装 system + 历史（裁剪后）。
func BuildRequestMessages(systemPrompt string, history []domain.LlmMessage, maxContext int) []domain.LlmMessage {
	messages := []domain.LlmMessage{{Role: "system", Content: &systemPrompt}}
	messages = append(messages, history...)
	return ContextWindowManager{}.Trim(messages, maxContext)
}

// SystemPromptBuilder 系统提示词构建。
type SystemPromptBuilder struct {
	Proxies      SystemPromptStore
	McpToolNames func() []string
}

// SystemPromptStore 系统提示词所需数据访问。
type SystemPromptStore interface {
	ListMemoriesScope(scope string) ([]MemorySummary, error)
	ListSkillsEnabled() ([]SkillSummary, error)
	ListSubAgentsEnabled() ([]SubAgentSummary, error)
}

type MemorySummary struct {
	Scope, Title, Content string
}

type SkillSummary struct {
	Name, Description string
}

type SubAgentSummary struct {
	Name, Description, InvocationRule string
}

const basePersona = `你是 Coding Agent：严谨、务实的本地代码执行代理。
- 使用工具完成用户的任务；文件操作与命令执行被限制在工作区内
- 需要独立完成的子任务时用 task 工具调用子代理
- 记忆可保存/检索（memory_save/memory_search）；技能按名加载（skill_load）
- 输出使用中文`

// Build 构建系统提示词。
func (b SystemPromptBuilder) Build(workspacePath string) string {
	var sb strings.Builder
	sb.WriteString(basePersona)
	sb.WriteString("\n\n## 工作区\n" + workspacePath)

	if b.Proxies != nil {
		if memories, err := b.Proxies.ListMemoriesScope(""); err == nil && len(memories) > 0 {
			sb.WriteString("\n\n## 记忆摘要\n（memory_search 检索完整内容，memory_save 保存新记忆）")
			for i, m := range memories {
				if i >= 30 {
					break
				}
				summary := m.Content
				if len(summary) > 200 {
					summary = summary[:200] + "…"
				}
				sb.WriteString(fmt.Sprintf("\n- [%s] **%s**: %s", m.Scope, m.Title, strings.ReplaceAll(summary, "\n", " ")))
			}
		}

		if skills, err := b.Proxies.ListSkillsEnabled(); err == nil && len(skills) > 0 {
			sb.WriteString("\n\n## 技能索引\n（skill_load 按名加载正文）")
			for _, s := range skills {
				sb.WriteString(fmt.Sprintf("\n- **%s**: %s", s.Name, s.Description))
			}
		}
	}

	if b.McpToolNames != nil {
		if names := b.McpToolNames(); len(names) > 0 {
			sb.WriteString("\n\n## MCP 工具（已连接）\n")
			for _, n := range names {
				sb.WriteString("\n- `" + n + "`")
			}
		}
	}

	if b.Proxies != nil {
		if agents, err := b.Proxies.ListSubAgentsEnabled(); err == nil && len(agents) > 0 {
			sb.WriteString("\n\n## 子代理及调用规则\n（task 工具调用，参数: {\"subagent\": 名称, \"input\": 任务输入}）")
			for _, a := range agents {
				sb.WriteString(fmt.Sprintf("\n- **%s**: %s", a.Name, a.Description))
				if a.InvocationRule != "" {
					sb.WriteString("\n  何时调用: " + a.InvocationRule)
				}
			}
		}
	}

	return sb.String()
}
