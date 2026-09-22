package api

import (
	"net/http"
	"strconv"

	"codingagent/internal/domain"
	"codingagent/internal/store"
)

// ---- sessions ----

type createSessionBody struct {
	Title           string  `json:"title"`
	ProjectID       *int64  `json:"projectId"`
	ProviderID      int64   `json:"providerId"`
	ModelID         string  `json:"modelId"`
	ReasoningEffort string  `json:"reasoningEffort"`
	WorkspacePath   *string `json:"workspacePath"`
}

func (s *Server) handleListSessions(w http.ResponseWriter, r *http.Request) {
	includeArchived := r.URL.Query().Get("includeArchived") == "true"
	list, err := s.Proxies.ListSessions(includeArchived)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleGetSession(w http.ResponseWriter, r *http.Request) {
	session, err := s.Proxies.GetSession(r.PathValue("id"))
	if err != nil {
		writeError(w, http.StatusNotFound, "会话不存在")
		return
	}
	writeJSON(w, http.StatusOK, session)
}

func (s *Server) handleCreateSession(w http.ResponseWriter, r *http.Request) {
	var body createSessionBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.ModelID == "" {
		writeError(w, http.StatusBadRequest, "modelId 不能为空")
		return
	}
	if _, err := s.Proxies.GetProvider(body.ProviderID); err != nil {
		writeError(w, http.StatusBadRequest, "提供商不存在")
		return
	}
	title := body.Title
	if title == "" {
		title = "新会话"
	}
	now := nowRFC3339()
	session := &domain.Session{
		ID:              newID(),
		Title:           title,
		ProjectID:       body.ProjectID,
		ProviderID:      body.ProviderID,
		ModelID:         body.ModelID,
		ReasoningEffort: domain.ParseEffort(body.ReasoningEffort),
		WorkspacePath:   body.WorkspacePath,
		Status:          domain.StatusActive,
		CreatedAt:       now,
		UpdatedAt:       now,
	}
	if err := s.Proxies.AddSession(session); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, session)
}

type updateSessionBody struct {
	Title           *string `json:"title"`
	ProviderID      *int64  `json:"providerId"`
	ModelID         *string `json:"modelId"`
	ReasoningEffort *string `json:"reasoningEffort"`
	WorkspacePath   *string `json:"workspacePath"`
	Status          *string `json:"status"`
}

func (s *Server) handleUpdateSession(w http.ResponseWriter, r *http.Request) {
	var body updateSessionBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	session, err := s.Proxies.GetSession(r.PathValue("id"))
	if err != nil {
		writeError(w, http.StatusNotFound, "会话不存在")
		return
	}
	if body.Title != nil {
		session.Title = *body.Title
	}
	if body.ProviderID != nil {
		session.ProviderID = *body.ProviderID
	}
	if body.ModelID != nil {
		session.ModelID = *body.ModelID
	}
	if body.ReasoningEffort != nil {
		session.ReasoningEffort = domain.ParseEffort(*body.ReasoningEffort)
	}
	if body.WorkspacePath != nil {
		session.WorkspacePath = body.WorkspacePath
	}
	if body.Status != nil && domain.SessionStatus(*body.Status) == domain.StatusArchived {
		session.Status = domain.StatusArchived
	}
	session.UpdatedAt = nowRFC3339()
	if err := s.Proxies.UpdateSession(session); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, session)
}

func (s *Server) handleDeleteSession(w http.ResponseWriter, r *http.Request) {
	if err := s.Proxies.DeleteSession(r.PathValue("id")); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *Server) handleListMessages(w http.ResponseWriter, r *http.Request) {
	if _, err := s.Proxies.GetSession(r.PathValue("id")); err != nil {
		writeError(w, http.StatusNotFound, "会话不存在")
		return
	}
	list, err := s.Proxies.ListMessages(r.PathValue("id"))
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

// ---- subagents ----

type subAgentBody struct {
	Name             string `json:"name"`
	Description      string `json:"description"`
	InvocationRule   string `json:"invocationRule"`
	SystemPrompt     string `json:"systemPrompt"`
	ProviderID       int64  `json:"providerId"`
	ModelID          string `json:"modelId"`
	ReasoningEffort  string `json:"reasoningEffort"`
	MaxTurns         int    `json:"maxTurns"`
	AllowedToolsJSON string `json:"allowedToolsJson"`
	Enabled          bool   `json:"enabled"`
}

func (s *Server) handleListSubAgents(w http.ResponseWriter, r *http.Request) {
	enabledOnly := r.URL.Query().Get("enabledOnly") == "true"
	list, err := s.Proxies.ListSubAgents(enabledOnly)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) subAgentFromBody(body subAgentBody) (*domain.SubAgent, string) {
	if body.Name == "" || body.ModelID == "" {
		return nil, "name 和 modelId 不能为空"
	}
	maxTurns := body.MaxTurns
	if maxTurns < 1 {
		maxTurns = 10
	}
	allowed := body.AllowedToolsJSON
	if allowed == "" {
		allowed = "[]"
	}
	return &domain.SubAgent{
		Name: body.Name, Description: body.Description, InvocationRule: body.InvocationRule,
		SystemPrompt: body.SystemPrompt, ProviderID: body.ProviderID, ModelID: body.ModelID,
		ReasoningEffort: domain.ParseEffort(body.ReasoningEffort), MaxTurns: maxTurns,
		AllowedToolsJSON: allowed, Enabled: body.Enabled,
	}, ""
}

func (s *Server) handleCreateSubAgent(w http.ResponseWriter, r *http.Request) {
	var body subAgentBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	sub, errMsg := s.subAgentFromBody(body)
	if errMsg != "" {
		writeError(w, http.StatusBadRequest, errMsg)
		return
	}
	if err := s.Proxies.AddSubAgent(sub); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, sub)
}

func (s *Server) handleUpdateSubAgent(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body subAgentBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if _, err := s.Proxies.GetSubAgent(id); err != nil {
		writeError(w, http.StatusNotFound, "子代理不存在")
		return
	}
	sub, errMsg := s.subAgentFromBody(body)
	if errMsg != "" {
		writeError(w, http.StatusBadRequest, errMsg)
		return
	}
	sub.ID = id
	if err := s.Proxies.UpdateSubAgent(sub); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, sub)
}

func (s *Server) handleDeleteSubAgent(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err := s.Proxies.DeleteSubAgent(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// ---- memories ----

type memoryBody struct {
	Scope   string  `json:"scope"`
	Title   string  `json:"title"`
	Content string  `json:"content"`
	Tags    *string `json:"tags"`
}

func (s *Server) handleListMemories(w http.ResponseWriter, r *http.Request) {
	scope := r.URL.Query().Get("scope")
	list, err := s.Proxies.ListMemories(scope)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleSearchMemories(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query().Get("q")
	list, err := s.Proxies.SearchMemories(q)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleCreateMemory(w http.ResponseWriter, r *http.Request) {
	var body memoryBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Title == "" || body.Content == "" {
		writeError(w, http.StatusBadRequest, "title 和 content 不能为空")
		return
	}
	m := &domain.Memory{Scope: domain.MemoryScope(scopeOf(body.Scope)), Title: body.Title, Content: body.Content, Tags: body.Tags, UpdatedAt: nowRFC3339()}
	if err := s.Proxies.AddMemory(m); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, m)
}

func (s *Server) handleUpdateMemory(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body memoryBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	m, err := s.Proxies.GetMemory(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "记忆不存在")
		return
	}
	m.Scope = domain.MemoryScope(scopeOf(body.Scope))
	m.Title = body.Title
	m.Content = body.Content
	m.Tags = body.Tags
	m.UpdatedAt = nowRFC3339()
	if err := s.Proxies.UpdateMemory(m); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, m)
}

func (s *Server) handleDeleteMemory(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err := s.Proxies.DeleteMemory(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func scopeOf(scope string) string {
	if scope == "project" {
		return "project"
	}
	return "global"
}

// ---- skills ----

type skillBody struct {
	Name         string `json:"name"`
	Description  string `json:"description"`
	Instructions string `json:"instructions"`
	Enabled      bool   `json:"enabled"`
}

func (s *Server) handleListSkills(w http.ResponseWriter, r *http.Request) {
	enabledOnly := r.URL.Query().Get("enabledOnly") == "true"
	list, err := s.Proxies.ListSkills(enabledOnly)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleCreateSkill(w http.ResponseWriter, r *http.Request) {
	var body skillBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Name == "" || body.Instructions == "" {
		writeError(w, http.StatusBadRequest, "name 和 instructions 不能为空")
		return
	}
	if _, err := s.Proxies.GetSkillByName(body.Name); err == nil {
		writeError(w, http.StatusConflict, "技能名已存在: "+body.Name)
		return
	}
	skill := &domain.Skill{Name: body.Name, Description: body.Description, Instructions: body.Instructions, Enabled: body.Enabled}
	if err := s.Proxies.AddSkill(skill); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, skill)
}

func (s *Server) handleUpdateSkill(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body skillBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	skill, err := s.Proxies.GetSkill(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "技能不存在")
		return
	}
	skill.Name = body.Name
	skill.Description = body.Description
	skill.Instructions = body.Instructions
	skill.Enabled = body.Enabled
	if err := s.Proxies.UpdateSkill(skill); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, skill)
}

func (s *Server) handleToggleSkill(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	skill, err := s.Proxies.GetSkill(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "技能不存在")
		return
	}
	skill.Enabled = !skill.Enabled
	if err := s.Proxies.UpdateSkill(skill); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, skill)
}

func (s *Server) handleDeleteSkill(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err := s.Proxies.DeleteSkill(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// ---- settings ----

func (s *Server) handleListSettings(w http.ResponseWriter, r *http.Request) {
	list, err := s.Proxies.AllSettings()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	// 不暴露 jwt_secret 等内部键
	out := make([]domain.Setting, 0, len(list))
	for _, item := range list {
		if len(item.Key) >= 4 && item.Key[:4] == "jwt_" {
			continue
		}
		out = append(out, item)
	}
	writeJSON(w, http.StatusOK, out)
}

func (s *Server) handleUpdateSetting(w http.ResponseWriter, r *http.Request) {
	var body domain.Setting
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Key == "" {
		writeError(w, http.StatusBadRequest, "key 不能为空")
		return
	}
	if len(body.Key) >= 4 && body.Key[:4] == "jwt_" {
		writeError(w, http.StatusBadRequest, "内部设置不允许修改")
		return
	}
	if err := s.Proxies.SetSetting(body.Key, body.Value); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"key": body.Key, "value": body.Value})
}

var _ = store.ErrNotFound // 保持 import
