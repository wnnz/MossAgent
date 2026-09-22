package api

import (
	"context"
	"net/http"
	"strconv"

	"codingagent/internal/domain"
	"codingagent/internal/llm"
	"codingagent/internal/proxy"
)

// ---- providers ----

func (s *Server) handleListProviders(w http.ResponseWriter, r *http.Request) {
	list, err := s.Proxies.ListProviders()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleGetProvider(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	p, err := s.Proxies.GetProvider(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "提供商不存在")
		return
	}
	writeJSON(w, http.StatusOK, p)
}

type providerBody struct {
	Name    string `json:"name"`
	Type    string `json:"type"`
	BaseURL string `json:"baseUrl"`
	APIKey  string `json:"apiKey"`
	ProxyID *int64 `json:"proxyId"`
	Enabled bool   `json:"enabled"`
}

func (s *Server) handleCreateProvider(w http.ResponseWriter, r *http.Request) {
	var body providerBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Name == "" || body.BaseURL == "" {
		writeError(w, http.StatusBadRequest, "name 和 baseUrl 不能为空")
		return
	}
	provider := &domain.Provider{
		Name: body.Name, Type: domain.ProviderType(body.Type), BaseURL: body.BaseURL,
		APIKey: body.APIKey, ProxyID: body.ProxyID, Enabled: body.Enabled,
		Models: []domain.ProviderModel{},
	}
	if err := s.Proxies.AddProvider(provider); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, provider)
}

func (s *Server) handleUpdateProvider(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body providerBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	provider, err := s.Proxies.GetProvider(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "提供商不存在")
		return
	}
	provider.Name = body.Name
	provider.Type = domain.ProviderType(body.Type)
	provider.BaseURL = body.BaseURL
	provider.APIKey = body.APIKey
	provider.ProxyID = body.ProxyID
	provider.Enabled = body.Enabled
	if err := s.Proxies.UpdateProvider(provider); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, provider)
}

func (s *Server) handleDeleteProvider(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err := s.Proxies.DeleteProvider(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *Server) handleRefreshModels(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	provider, err := s.Proxies.GetProvider(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "提供商不存在")
		return
	}

	client, err := llm.NewFactory(s.Proxies).ClientForPublic(provider)
	if err != nil {
		writeError(w, http.StatusBadRequest, err.Error())
		return
	}
	remoteIDs, err := llm.FetchModelIDs(context.Background(), provider, client)
	if err != nil {
		writeError(w, http.StatusBadRequest, err.Error())
		return
	}

	existing := map[string]bool{}
	for _, m := range provider.Models {
		existing[m.ModelID] = true
	}
	remote := map[string]bool{}
	for _, mid := range remoteIDs {
		remote[mid] = true
	}

	// 删除远端已不存在的非自定义模型；新增缺失的
	for _, m := range provider.Models {
		if !m.IsCustom && !remote[m.ModelID] {
			_ = s.Proxies.DeleteModel(id, m.ID)
		}
	}
	added := 0
	for _, mid := range remoteIDs {
		if !existing[mid] {
			display := mid
			_ = s.Proxies.AddModel(&domain.ProviderModel{ProviderID: id, ModelID: mid, DisplayName: display})
			added++
		}
	}

	updated, _ := s.Proxies.GetProvider(id)
	models := []domain.ProviderModel{}
	if updated != nil {
		models = updated.Models
	}
	writeJSON(w, http.StatusOK, map[string]any{"added": added, "updated": -len(provider.Models) + len(models), "models": models})
}

type modelBody struct {
	ModelID                string `json:"modelId"`
	DisplayName            string `json:"displayName"`
	SupportsTools          bool   `json:"supportsTools"`
	SupportsReasoning      bool   `json:"supportsReasoning"`
	DefaultReasoningEffort string `json:"defaultReasoningEffort"`
	MaxContextTokens       int    `json:"maxContextTokens"`
	MaxOutputTokens        int    `json:"maxOutputTokens"`
	IsCustom               bool   `json:"isCustom"`
}

func (s *Server) handleAddModel(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body modelBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.ModelID == "" {
		writeError(w, http.StatusBadRequest, "modelId 不能为空")
		return
	}
	display := body.DisplayName
	if display == "" {
		display = body.ModelID
	}
	effort := domain.ParseEffort(body.DefaultReasoningEffort)
	maxCtx := body.MaxContextTokens
	if maxCtx == 0 {
		maxCtx = 128000
	}
	maxOut := body.MaxOutputTokens
	if maxOut == 0 {
		maxOut = 8192
	}
	m := &domain.ProviderModel{
		ProviderID: id, ModelID: body.ModelID, DisplayName: display,
		SupportsTools: body.SupportsTools, SupportsReasoning: body.SupportsReasoning,
		DefaultReasoningEffort: effort, MaxContextTokens: maxCtx, MaxOutputTokens: maxOut, IsCustom: true,
	}
	if err := s.Proxies.AddModel(m); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, m)
}

func (s *Server) handleUpdateModel(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	mid, _ := strconv.ParseInt(r.PathValue("mid"), 10, 64)
	var body modelBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	m, err := s.Proxies.GetModel(id, mid)
	if err != nil {
		writeError(w, http.StatusNotFound, "模型不存在")
		return
	}
	display := body.DisplayName
	if display == "" {
		display = body.ModelID
	}
	m.ModelID = body.ModelID
	m.DisplayName = display
	m.SupportsTools = body.SupportsTools
	m.SupportsReasoning = body.SupportsReasoning
	m.DefaultReasoningEffort = domain.ParseEffort(body.DefaultReasoningEffort)
	m.MaxContextTokens = body.MaxContextTokens
	m.MaxOutputTokens = body.MaxOutputTokens
	m.IsCustom = body.IsCustom
	if err := s.Proxies.UpdateModel(m); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, m)
}

func (s *Server) handleDeleteModel(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	mid, _ := strconv.ParseInt(r.PathValue("mid"), 10, 64)
	if err := s.Proxies.DeleteModel(id, mid); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// ---- proxies ----

type proxyBody struct {
	Name     string  `json:"name"`
	Scheme   string  `json:"scheme"`
	Host     string  `json:"host"`
	Port     int     `json:"port"`
	Username *string `json:"username"`
	Password *string `json:"password"`
	Enabled  bool    `json:"enabled"`
}

func (s *Server) handleListProxies(w http.ResponseWriter, r *http.Request) {
	list, err := s.Proxies.ListProxies()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleCreateProxy(w http.ResponseWriter, r *http.Request) {
	var body proxyBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Name == "" || body.Host == "" || body.Port <= 0 {
		writeError(w, http.StatusBadRequest, "name、host 必填且 port > 0")
		return
	}
	p := &domain.Proxy{Name: body.Name, Scheme: domain.ProxyScheme(body.Scheme), Host: body.Host, Port: body.Port, Username: body.Username, Password: body.Password, Enabled: body.Enabled}
	if err := s.Proxies.AddProxy(p); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, p)
}

func (s *Server) handleUpdateProxy(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body proxyBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	p, err := s.Proxies.GetProxy(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "代理不存在")
		return
	}
	p.Name = body.Name
	p.Scheme = domain.ProxyScheme(body.Scheme)
	p.Host = body.Host
	p.Port = body.Port
	p.Username = body.Username
	p.Password = body.Password
	p.Enabled = body.Enabled
	if err := s.Proxies.UpdateProxy(p); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, p)
}

func (s *Server) handleDeleteProxy(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err := s.Proxies.DeleteProxy(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *Server) handleTestProxy(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	p, err := s.Proxies.GetProxy(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "代理不存在")
		return
	}
	success, message, latency := proxy.TestProxy(p)
	writeJSON(w, http.StatusOK, map[string]any{"success": success, "message": message, "latencyMs": latency})
}
