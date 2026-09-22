package api

import (
	"encoding/json"
	"net/http"

	"codingagent/internal/agent"
	"codingagent/internal/domain"
)

// ---- chat (SSE) ----

type chatBody struct {
	Message         string  `json:"message"`
	ReasoningEffort *string `json:"reasoningEffort"`
}

// 事件类型 → SSE 事件名（与前端契约一致）。
func eventName(t domain.AgentEventType) string {
	switch t {
	case domain.EvtTurnStarted:
		return "turn_started"
	case domain.EvtMessageDelta:
		return "message_delta"
	case domain.EvtToolCallStarted:
		return "tool_call_started"
	case domain.EvtToolCallFinished:
		return "tool_call_finished"
	case domain.EvtTurnCompleted:
		return "turn_completed"
	case domain.EvtCancelled:
		return "cancelled"
	case domain.EvtError:
		return "error"
	}
	return "message_delta"
}

func (s *Server) handleChat(w http.ResponseWriter, r *http.Request) {
	sessionID := r.PathValue("id")
	var body chatBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Message == "" {
		writeError(w, http.StatusBadRequest, "message 不能为空")
		return
	}
	session, err := s.Proxies.GetSession(sessionID)
	if err != nil {
		writeError(w, http.StatusNotFound, "会话不存在")
		return
	}

	// 同一会话已有运行中的对话时拒绝并发（owner 语义）
	if s.isRunning(sessionID) {
		writeError(w, http.StatusConflict, "该会话已有进行中的对话")
		return
	}

	// 默认工作区兜底
	if session.WorkspacePath == nil || *session.WorkspacePath == "" {
		root := s.WorkspaceRoot
		session.WorkspacePath = &root
	}

	var effortOverride *domain.ReasoningEffort
	if body.ReasoningEffort != nil && *body.ReasoningEffort != "" {
		effort := domain.ParseEffort(*body.ReasoningEffort)
		effortOverride = &effort
	}

	flusher, ok := w.(http.Flusher)
	if !ok {
		writeError(w, http.StatusInternalServerError, "当前连接不支持流式输出")
		return
	}

	w.Header().Set("Content-Type", "text/event-stream")
	w.Header().Set("Cache-Control", "no-cache")
	w.Header().Set("Connection", "keep-alive")
	w.WriteHeader(http.StatusOK)
	flusher.Flush()

	runHandle := s.startRun(sessionID)
	defer s.endRun(sessionID, runHandle)

	opts := agent.DefaultOptions()
	events := make(chan domain.AgentEvent, 128)

	// 客户端断开 → 取消运行（消息已在 Loop 内落库，持久化一致性）
	go func() {
		<-r.Context().Done()
		runHandle.cancel() // 幂等
	}()

	go s.Loop.Run(*session, body.Message, effortOverride, opts, events, runHandle.ctx.Done())

	for evt := range events {
		// 会话被 stop 端点取消时 cancel 关闭，Loop 的 emit 已停；此处仅透传
		sseWrite(w, flusher, eventName(evt.Type), evt.Data)
	}

	// 刷新会话时间戳（消息已在 Loop 内落库）
	if latest, err := s.Proxies.GetSession(sessionID); err == nil {
		latest.UpdatedAt = nowRFC3339()
		_ = s.Proxies.UpdateSession(latest)
	}
}

func sseWrite(w http.ResponseWriter, flusher http.Flusher, eventName string, data any) {
	payload, _ := json.Marshal(data)
	_, _ = w.Write([]byte("event: " + eventName + "\ndata: " + string(payload) + "\n\n"))
	flusher.Flush()
}

// ---- stop ----

func (s *Server) handleStop(w http.ResponseWriter, r *http.Request) {
	sessionID := r.PathValue("id")
	cancelled := s.tryCancel(sessionID)
	writeJSON(w, http.StatusOK, map[string]any{"cancelled": cancelled})
}

var _ = agent.DefaultOptions // 保持 import
