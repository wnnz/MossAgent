// Package api：HTTP API 层（net/http Go 1.22 路由）。
package api

import (
	"context"
	"encoding/json"
	"net/http"
	"os"
	"strings"
	"sync"

	"codingagent/internal/agent"
	"codingagent/internal/domain"
	"codingagent/internal/mcp"
	"codingagent/internal/security"
	"codingagent/internal/store"
)

// Server HTTP 服务（显式装配，无容器）。
type Server struct {
	Proxies *store.DB
	Loop    *agent.Loop
	Tokens  *security.TokenService
	Bridge  *mcp.Bridge
	// TaskRunner 子代理执行函数（main 注入）
	TaskRunner func(subagent, input string) string
	// WorkspaceRoot 默认工作区
	WorkspaceRoot string
	// FrontendDist 前端产物目录（存在时托管）
	FrontendDist string

	runningMu sync.Mutex
	running   map[string]*run // 会话 → 当前运行取消句柄
}

const cookieName = "codingagent_auth"

var anonymousPaths = map[string]bool{
	"/api/auth/status": true,
	"/api/auth/setup":  true,
	"/api/auth/login":  true,
}

// Routes 构建全部路由（Go 1.22 method+path 模式）。
func (s *Server) Routes() http.Handler {
	// 运行管理 map 惰性初始化（main 装配后仅调用一次）
	s.runningMu.Lock()
	if s.running == nil {
		s.running = map[string]*run{}
	}
	s.runningMu.Unlock()

	mux := http.NewServeMux()

	// auth
	mux.HandleFunc("GET /api/auth/status", s.handleAuthStatus)
	mux.HandleFunc("POST /api/auth/setup", s.handleAuthSetup)
	mux.HandleFunc("POST /api/auth/login", s.handleAuthLogin)
	mux.HandleFunc("POST /api/auth/logout", s.handleAuthLogout)
	mux.HandleFunc("PUT /api/auth/password", s.handleAuthPassword)

	// providers
	mux.HandleFunc("GET /api/providers", s.handleListProviders)
	mux.HandleFunc("POST /api/providers", s.handleCreateProvider)
	mux.HandleFunc("GET /api/providers/{id}", s.handleGetProvider)
	mux.HandleFunc("PUT /api/providers/{id}", s.handleUpdateProvider)
	mux.HandleFunc("DELETE /api/providers/{id}", s.handleDeleteProvider)
	mux.HandleFunc("POST /api/providers/{id}/models/refresh", s.handleRefreshModels)
	mux.HandleFunc("POST /api/providers/{id}/models", s.handleAddModel)
	mux.HandleFunc("PUT /api/providers/{id}/models/{mid}", s.handleUpdateModel)
	mux.HandleFunc("DELETE /api/providers/{id}/models/{mid}", s.handleDeleteModel)

	// proxies
	mux.HandleFunc("GET /api/proxies", s.handleListProxies)
	mux.HandleFunc("POST /api/proxies", s.handleCreateProxy)
	mux.HandleFunc("PUT /api/proxies/{id}", s.handleUpdateProxy)
	mux.HandleFunc("DELETE /api/proxies/{id}", s.handleDeleteProxy)
	mux.HandleFunc("POST /api/proxies/{id}/test", s.handleTestProxy)

	// sessions
	mux.HandleFunc("GET /api/sessions", s.handleListSessions)
	mux.HandleFunc("POST /api/sessions", s.handleCreateSession)
	mux.HandleFunc("GET /api/sessions/{id}", s.handleGetSession)
	mux.HandleFunc("PATCH /api/sessions/{id}", s.handleUpdateSession)
	mux.HandleFunc("DELETE /api/sessions/{id}", s.handleDeleteSession)
	mux.HandleFunc("GET /api/sessions/{id}/messages", s.handleListMessages)

	// chat
	mux.HandleFunc("POST /api/sessions/{id}/chat", s.handleChat)
	mux.HandleFunc("POST /api/sessions/{id}/stop", s.handleStop)

	// subagents
	mux.HandleFunc("GET /api/subagents", s.handleListSubAgents)
	mux.HandleFunc("POST /api/subagents", s.handleCreateSubAgent)
	mux.HandleFunc("PUT /api/subagents/{id}", s.handleUpdateSubAgent)
	mux.HandleFunc("DELETE /api/subagents/{id}", s.handleDeleteSubAgent)

	// memories
	mux.HandleFunc("GET /api/memories", s.handleListMemories)
	mux.HandleFunc("GET /api/memories/search", s.handleSearchMemories)
	mux.HandleFunc("POST /api/memories", s.handleCreateMemory)
	mux.HandleFunc("PUT /api/memories/{id}", s.handleUpdateMemory)
	mux.HandleFunc("DELETE /api/memories/{id}", s.handleDeleteMemory)

	// skills
	mux.HandleFunc("GET /api/skills", s.handleListSkills)
	mux.HandleFunc("POST /api/skills", s.handleCreateSkill)
	mux.HandleFunc("PUT /api/skills/{id}", s.handleUpdateSkill)
	mux.HandleFunc("PATCH /api/skills/{id}/toggle", s.handleToggleSkill)
	mux.HandleFunc("DELETE /api/skills/{id}", s.handleDeleteSkill)

	// mcp
	mux.HandleFunc("GET /api/mcp/servers", s.handleListMcpServers)
	mux.HandleFunc("POST /api/mcp/servers", s.handleCreateMcpServer)
	mux.HandleFunc("PUT /api/mcp/servers/{id}", s.handleUpdateMcpServer)
	mux.HandleFunc("DELETE /api/mcp/servers/{id}", s.handleDeleteMcpServer)
	mux.HandleFunc("POST /api/mcp/servers/{id}/connect", s.handleConnectMcp)
	mux.HandleFunc("POST /api/mcp/servers/{id}/disconnect", s.handleDisconnectMcp)
	mux.HandleFunc("GET /api/mcp/servers/{id}/tools", s.handleListMcpTools)
	mux.HandleFunc("POST /api/mcp/servers/{id}/tools/{tool}/call", s.handleCallMcpTool)

	// projects
	mux.HandleFunc("GET /api/projects", s.handleListProjects)
	mux.HandleFunc("POST /api/projects", s.handleCreateProject)
	mux.HandleFunc("DELETE /api/projects/{id}", s.handleDeleteProject)
	mux.HandleFunc("GET /api/projects/{id}/branch", s.handleProjectBranch)

	// settings
	mux.HandleFunc("GET /api/settings", s.handleListSettings)
	mux.HandleFunc("PUT /api/settings", s.handleUpdateSetting)

	// 生产托管前端 dist（存在时），SPA fallback
	if s.FrontendDist != "" {
		fs := http.FileServer(http.Dir(s.FrontendDist))
		mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
			if strings.HasPrefix(r.URL.Path, "/api/") {
				http.NotFound(w, r)
				return
			}
			// 静态文件存在则返回，否则 SPA fallback 到 index.html
			full := s.FrontendDist + r.URL.Path
			if r.URL.Path != "/" {
				if exists, _ := fileExists(full); exists {
					fs.ServeHTTP(w, r)
					return
				}
			}
			http.ServeFile(w, r, s.FrontendDist+"/index.html")
		})
	}

	return s.authMiddleware(mux)
}

func fileExists(path string) (bool, error) {
	_, err := os.Stat(path)
	if err != nil {
		return false, err
	}
	return true, nil
}

// authMiddleware JWT Cookie 认证门禁：匿名表放行，其余 /api 需认证。
func (s *Server) authMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasPrefix(r.URL.Path, "/api/") || r.URL.Path == "/api" {
			normalized := strings.TrimRight(r.URL.Path, "/")
			if normalized == "/api" {
				normalized = "/api"
			}
			if !anonymousPaths[normalized] && !anonymousPaths[r.URL.Path] && !s.isAuthenticated(r) {
				w.WriteHeader(http.StatusUnauthorized)
				_ = json.NewEncoder(w).Encode(map[string]any{"message": "未认证"})
				return
			}
		}
		next.ServeHTTP(w, r)
	})
}

func (s *Server) isAuthenticated(r *http.Request) bool {
	c, err := r.Cookie(cookieName)
	if err != nil || c.Value == "" {
		return false
	}
	return s.Tokens.TryValidate(c.Value)
}

func (s *Server) setCookie(w http.ResponseWriter, token string) {
	http.SetCookie(w, &http.Cookie{
		Name:     cookieName,
		Value:    token,
		Path:     "/",
		HttpOnly: true,
		SameSite: http.SameSiteLaxMode,
	})
}

// ---- JSON 辅助 ----

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}

func writeError(w http.ResponseWriter, status int, message string) {
	writeJSON(w, status, map[string]any{"message": message})
}

func decodeBody(r *http.Request, v any) error {
	if err := json.NewDecoder(r.Body).Decode(v); err != nil {
		return err
	}
	return nil
}

func nowRFC3339() string { return timeNow().UTC().Format(timeRFC3339) }

// run 一次会话运行的取消句柄（context cancel 幂等，无双 close 风险）。
type run struct {
	ctx    context.Context
	cancel context.CancelFunc
}

// ---- 会话运行管理（owner 语义） ----

func (s *Server) isRunning(sessionID string) bool {
	s.runningMu.Lock()
	defer s.runningMu.Unlock()
	_, ok := s.running[sessionID]
	return ok
}

func (s *Server) startRun(sessionID string) *run {
	ctx, cancel := context.WithCancel(context.Background())
	r := &run{ctx: ctx, cancel: cancel}
	s.runningMu.Lock()
	if old, ok := s.running[sessionID]; ok {
		old.cancel() // 竞态兜底：旧运行取消（幂等）
	}
	s.running[sessionID] = r
	s.runningMu.Unlock()
	return r
}

func (s *Server) endRun(sessionID string, r *run) {
	s.runningMu.Lock()
	if current, ok := s.running[sessionID]; ok && current == r {
		delete(s.running, sessionID)
	}
	s.runningMu.Unlock()
}

func (s *Server) tryCancel(sessionID string) bool {
	s.runningMu.Lock()
	defer s.runningMu.Unlock()
	if r, ok := s.running[sessionID]; ok {
		r.cancel()
		return true
	}
	return false
}

// deref 宽松取字符串指针。
func deref(p *string) string {
	if p == nil {
		return ""
	}
	return *p
}

var _ = domain.EvtCancelled // 保持 import（事件名在 chat.go 映射）
