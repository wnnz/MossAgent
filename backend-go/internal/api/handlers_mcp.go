package api

import (
	"encoding/json"
	"net/http"
	"strconv"

	"codingagent/internal/domain"
)

// ---- mcp ----

type mcpServerBody struct {
	Name        string  `json:"name"`
	Transport   string  `json:"transport"`
	Command     *string `json:"command"`
	ArgsJSON    *string `json:"argsJson"`
	EnvJSON     *string `json:"envJson"`
	URL         *string `json:"url"`
	HeadersJSON *string `json:"headersJson"`
	Enabled     bool    `json:"enabled"`
	AutoConnect bool    `json:"autoConnect"`
}

func (s *Server) handleListMcpServers(w http.ResponseWriter, r *http.Request) {
	list, err := s.Proxies.ListMcpServers()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleCreateMcpServer(w http.ResponseWriter, r *http.Request) {
	var body mcpServerBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Name == "" {
		writeError(w, http.StatusBadRequest, "name 不能为空")
		return
	}
	transport := domain.McpTransport(body.Transport)
	if transport == domain.TransportStdio && (body.Command == nil || *body.Command == "") {
		writeError(w, http.StatusBadRequest, "stdio 传输必须提供 command")
		return
	}
	if transport == domain.TransportHTTP && (body.URL == nil || *body.URL == "") {
		writeError(w, http.StatusBadRequest, "http 传输必须提供 url")
		return
	}
	server := &domain.McpServer{
		Name: body.Name, Transport: transport, Command: body.Command, ArgsJSON: body.ArgsJSON,
		EnvJSON: body.EnvJSON, URL: body.URL, HeadersJSON: body.HeadersJSON,
		Enabled: body.Enabled, AutoConnect: body.AutoConnect,
	}
	if err := s.Proxies.AddMcpServer(server); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, server)
}

func (s *Server) handleUpdateMcpServer(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	var body mcpServerBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if _, err := s.Proxies.GetMcpServer(id); err != nil {
		writeError(w, http.StatusNotFound, "服务器不存在")
		return
	}
	// 配置变更后断开旧连接
	s.Bridge.Registry.Disconnect(id)
	server := &domain.McpServer{
		ID: id, Name: body.Name, Transport: domain.McpTransport(body.Transport),
		Command: body.Command, ArgsJSON: body.ArgsJSON, EnvJSON: body.EnvJSON, URL: body.URL,
		HeadersJSON: body.HeadersJSON, Enabled: body.Enabled, AutoConnect: body.AutoConnect,
	}
	if err := s.Proxies.UpdateMcpServer(server); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, server)
}

func (s *Server) handleDeleteMcpServer(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	s.Bridge.Registry.Disconnect(id)
	if err := s.Proxies.DeleteMcpServer(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *Server) handleConnectMcp(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	server, err := s.Proxies.GetMcpServer(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "服务器不存在")
		return
	}
	conn, err := s.Bridge.Registry.GetOrConnect(*server)
	if err != nil {
		writeError(w, http.StatusBadRequest, "连接失败: "+err.Error())
		return
	}
	infos, err := conn.ListTools()
	if err != nil {
		s.Bridge.Registry.Disconnect(id)
		writeError(w, http.StatusBadRequest, "获取工具列表失败: "+err.Error())
		return
	}
	if err := s.Proxies.ReplaceMcpTools(id, infos); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	tools, _ := s.Proxies.ListMcpTools(id)
	writeJSON(w, http.StatusOK, tools)
}

func (s *Server) handleDisconnectMcp(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	s.Bridge.Registry.Disconnect(id)
	writeJSON(w, http.StatusOK, map[string]any{"message": "已断开"})
}

func (s *Server) handleListMcpTools(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if _, err := s.Proxies.GetMcpServer(id); err != nil {
		writeError(w, http.StatusNotFound, "服务器不存在")
		return
	}
	tools, err := s.Proxies.ListMcpTools(id)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, tools)
}

func (s *Server) handleCallMcpTool(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	toolName := r.PathValue("tool")
	if _, err := s.Proxies.GetMcpServer(id); err != nil {
		writeError(w, http.StatusNotFound, "服务器不存在")
		return
	}
	conn := s.Bridge.Registry.Get(id)
	if conn == nil {
		writeError(w, http.StatusBadRequest, "服务器未连接，请先连接")
		return
	}
	var body struct {
		Arguments map[string]any `json:"arguments"`
	}
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	argsJSON := "{}"
	if body.Arguments != nil {
		if b, err := json.Marshal(body.Arguments); err == nil {
			argsJSON = string(b)
		}
	}
	result, err := conn.CallTool(toolName, argsJSON)
	if err != nil {
		writeJSON(w, http.StatusOK, map[string]any{"success": false, "result": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"success": true, "result": result})
}
