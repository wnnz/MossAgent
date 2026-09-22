package agent

import (
	"context"
	"encoding/json"
	"time"

	"codingagent/internal/domain"
	"codingagent/internal/store"
)

func nowRFC3339() string { return time.Now().UTC().Format(time.RFC3339) }

// StreamFactory LLM 流式客户端工厂（llm.Factory 实现）。
type StreamFactory interface {
	Create(p *domain.Provider) (func(context.Context, domain.LlmRequest, chan<- domain.LlmStreamEvent), error)
}

// Options Agent 循环选项（子代理运行时限制工具与轮次）。
type Options struct {
	MaxTurns             int
	AllowedToolNames     map[string]bool // nil 表示不限制
	SystemPromptOverride string
	PersistMessages      bool
}

// DefaultOptions 默认选项（最大轮次 25）。
func DefaultOptions() Options { return Options{MaxTurns: 25, PersistMessages: true} }

// Loop Agent 循环：系统提示词 → 流式 LLM → tool_calls → 工具执行 → 回填，直至无调用或达最大轮次。
type Loop struct {
	Proxies     *store.DB
	LLM         StreamFactory
	Registry    *Registry
	Context     ContextWindowManager
	BuildPrompt func(workspacePath string) string
}

// ToLlmMessage 持久化消息 → LLM 消息。
func ToLlmMessage(m domain.ChatMessage) domain.LlmMessage {
	out := domain.LlmMessage{Role: string(m.Role), Content: &m.Content, ToolCallID: m.ToolCallID, Name: m.Name}
	if m.ToolCallsJSON != nil {
		var calls []domain.LlmToolCall
		if err := json.Unmarshal([]byte(*m.ToolCallsJSON), &calls); err == nil && len(calls) > 0 {
			out.ToolCalls = calls
		}
	}
	return out
}

// Run 运行循环，事件经 channel 流式产出（channel 关闭即结束）。
func (l *Loop) Run(session domain.Session, userMessage string, effortOverride *domain.ReasoningEffort, opts Options, events chan<- domain.AgentEvent, cancel <-chan struct{}) {
	defer close(events)

	emit := func(t domain.AgentEventType, data any) bool {
		select {
		case events <- domain.AgentEvent{Type: t, Data: data}:
			return true
		case <-cancel:
			return false
		}
	}

	provider, err := l.Proxies.GetProvider(session.ProviderID)
	if err != nil {
		emit(domain.EvtError, map[string]any{"message": "提供商不存在: " + err.Error()})
		return
	}

	// 模型解析（回退时同步实际请求的模型名）
	requestModel := session.ModelID
	var model *domain.ProviderModel
	for i := range provider.Models {
		if provider.Models[i].ModelID == session.ModelID {
			model = &provider.Models[i]
			break
		}
	}
	if model == nil && len(provider.Models) > 0 {
		model = &provider.Models[0]
		requestModel = model.ModelID
	}

	// 历史消息 + 用户消息落库
	history := []domain.LlmMessage{}
	if opts.PersistMessages {
		stored, err := l.Proxies.ListMessages(session.ID)
		if err != nil {
			emit(domain.EvtError, map[string]any{"message": "读取历史失败: " + err.Error()})
			return
		}
		for _, m := range stored {
			history = append(history, ToLlmMessage(m))
		}
		_ = l.Proxies.AddMessage(&domain.ChatMessage{
			SessionID: session.ID, Role: domain.RoleUser, Content: userMessage, CreatedAt: nowRFC3339(),
		})
	}
	history = append(history, domain.LlmMessage{Role: "user", Content: &userMessage})

	workspacePath := ""
	if session.WorkspacePath != nil {
		workspacePath = *session.WorkspacePath
	}

	systemPrompt := opts.SystemPromptOverride
	if systemPrompt == "" {
		if l.BuildPrompt != nil && workspacePath != "" {
			systemPrompt = l.BuildPrompt(workspacePath)
		} else {
			systemPrompt = "你是 Coding Agent。"
		}
	}

	maxContext := 128_000
	maxOutput := 8_192
	if model != nil {
		if model.MaxContextTokens > 0 {
			maxContext = model.MaxContextTokens
		}
		if model.MaxOutputTokens > 0 {
			maxOutput = model.MaxOutputTokens
		}
	}

	effort := session.ReasoningEffort
	if effortOverride != nil {
		effort = *effortOverride
	}

	maxTurns := opts.MaxTurns
	if maxTurns < 1 {
		maxTurns = 1
	}

	for turn := 1; turn <= maxTurns; turn++ {
		if !emit(domain.EvtTurnStarted, map[string]any{"turnIndex": turn}) {
			return
		}

		// 组装 LLM 请求
		messages := BuildRequestMessages(systemPrompt, history, maxContext)
		var toolDefs []domain.LlmToolDefinition
		if model == nil || model.SupportsTools {
			toolDefs = l.Registry.Definitions()
		}

		// 找到提供商的流式函数
		streamFn, err := l.LLM.Create(provider)
		if err != nil {
			emit(domain.EvtError, map[string]any{"message": err.Error()})
			return
		}

		req := domain.LlmRequest{
			Model:           requestModel,
			Messages:        messages,
			Tools:           toolDefs,
			ReasoningEffort: effort,
			MaxOutputTokens: maxOutput,
		}

		// 流式消费（ctx 取消传播）
		streamCtx, stop := context.WithCancel(context.Background())
		llmEvents := make(chan domain.LlmStreamEvent, 64)
		go streamFn(streamCtx, req, llmEvents)

		var assistantText string
		var toolCalls []domain.LlmToolCall
		var llmErr string

		for evt := range llmEvents {
			switch evt.Type {
			case domain.LlmDelta:
				assistantText += evt.Delta
				if !emit(domain.EvtMessageDelta, map[string]any{"content": evt.Delta}) {
					stop()
					return
				}
			case domain.LlmToolCallEvt:
				toolCalls = append(toolCalls, *evt.ToolCall)
			case domain.LlmError:
				llmErr = evt.Error
			case domain.LlmCompleted:
			}
		}
		stop()

		if llmErr != "" {
			emit(domain.EvtError, map[string]any{"message": llmErr})
			return
		}

		// assistant 消息落库
		if opts.PersistMessages {
			var toolCallsJSON *string
			if len(toolCalls) > 0 {
				b, _ := json.Marshal(toolCalls)
				s := string(b)
				toolCallsJSON = &s
			}
			_ = l.Proxies.AddMessage(&domain.ChatMessage{
				SessionID: session.ID, Role: domain.RoleAssistant, Content: assistantText,
				ToolCallsJSON: toolCallsJSON, CreatedAt: nowRFC3339(),
			})
		}
		var assistantToolCalls []domain.LlmToolCall
		if len(toolCalls) > 0 {
			assistantToolCalls = toolCalls
		}
		history = append(history, domain.LlmMessage{Role: "assistant", Content: &assistantText, ToolCalls: assistantToolCalls})

		if len(toolCalls) == 0 {
			emit(domain.EvtTurnCompleted, map[string]any{"content": assistantText})
			return
		}

		// 顺序执行工具调用
		toolCtx := domain.ToolContext{
			SessionID:        session.ID,
			WorkspacePath:    workspacePath,
			AllowedToolNames: opts.AllowedToolNames,
		}
		allTools := l.Registry.All()

		for _, call := range toolCalls {
			if !emit(domain.EvtToolCallStarted, map[string]any{
				"id": call.ID, "name": call.Name, "arguments": call.ParseArguments(),
			}) {
				return
			}

			result := executeTool(allTools, call, toolCtx)

			if !emit(domain.EvtToolCallFinished, map[string]any{
				"id": call.ID, "name": call.Name, "result": result.Output, "isError": !result.Success,
			}) {
				return
			}

			if opts.PersistMessages {
				name := call.Name
				id := call.ID
				_ = l.Proxies.AddMessage(&domain.ChatMessage{
					SessionID: session.ID, Role: domain.RoleTool, Content: result.Output,
					ToolCallID: &id, Name: &name, CreatedAt: nowRFC3339(),
				})
			}
			history = append(history, domain.LlmMessage{
				Role: "tool", Content: &result.Output, ToolCallID: &call.ID, Name: &call.Name,
			})
		}

		if turn == maxTurns {
			emit(domain.EvtError, map[string]any{"message": "已达最大轮次，强制结束"})
			return
		}
	}
}

func executeTool(all []domain.Tool, call domain.LlmToolCall, ctx domain.ToolContext) domain.ToolResult {
	for _, t := range all {
		if t.Name() == call.Name {
			if ctx.AllowedToolNames != nil && !ctx.AllowedToolNames[call.Name] {
				return domain.ToolResult{Success: false, Output: "工具不在允许列表中: " + call.Name}
			}
			return t.Execute(call.ArgumentsJSON, ctx)
		}
	}
	return domain.ToolResult{Success: false, Output: "工具不存在: " + call.Name}
}
