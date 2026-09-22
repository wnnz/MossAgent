package mcp

import (
	"sync"

	"codingagent/internal/domain"
	"codingagent/internal/store"
)

// Registry 维护已连接的 MCP 连接（进程单例）。connectLock 互斥建立连接（Initialize 含等待，串行化）。
type Registry struct {
	mu          sync.Mutex
	connectLock sync.Mutex
	connections map[int64]domain.McpConnection
}

func NewRegistry() *Registry {
	return &Registry{connections: map[int64]domain.McpConnection{}}
}

// GetOrConnect 获取或建立连接（连接失败返回错误，不缓存）。
func (r *Registry) GetOrConnect(server domain.McpServer) (domain.McpConnection, error) {
	r.mu.Lock()
	if c, ok := r.connections[server.ID]; ok && c.IsConnected() {
		r.mu.Unlock()
		return c, nil
	}
	// 旧连接清理（锁内引用，锁外 Close）
	var old domain.McpConnection
	if c, ok := r.connections[server.ID]; ok {
		old = c
		delete(r.connections, server.ID)
	}
	r.mu.Unlock()
	if old != nil {
		old.Close()
	}

	// Initialize 含进程/网络等待：与其它建立请求互斥（聊天轮次 Registry.Get 不抢此锁）
	r.connectLock.Lock()
	defer r.connectLock.Unlock()

	var conn domain.McpConnection
	if domain.McpTransport(server.Transport) == domain.TransportHTTP {
		conn = NewHTTP(server)
	} else {
		conn = NewStdio(server)
	}
	if err := conn.Initialize(); err != nil {
		conn.Close()
		return nil, err
	}

	r.mu.Lock()
	r.connections[server.ID] = conn
	r.mu.Unlock()
	return conn, nil
}

// Get 获取已连接实例（不建立）。
func (r *Registry) Get(serverID int64) domain.McpConnection {
	r.mu.Lock()
	defer r.mu.Unlock()
	if c, ok := r.connections[serverID]; ok && c.IsConnected() {
		return c
	}
	return nil
}

// Disconnect 断开并清理。
func (r *Registry) Disconnect(serverID int64) {
	r.mu.Lock()
	c, ok := r.connections[serverID]
	if ok {
		delete(r.connections, serverID)
	}
	r.mu.Unlock()
	if ok {
		c.Close()
	}
}

// DisconnectAll 全部断开（进程退出时）。
func (r *Registry) DisconnectAll() {
	r.mu.Lock()
	ids := make([]int64, 0, len(r.connections))
	for id := range r.connections {
		ids = append(ids, id)
	}
	connections := r.connections
	r.connections = map[int64]domain.McpConnection{}
	r.mu.Unlock()
	for _, id := range ids {
		connections[id].Close()
	}
}

// Bridge 把已连接 MCP 服务器的缓存工具桥接为 ITool（命名 mcp__<server>__<tool>）。
type Bridge struct {
	Proxies   *store.DB
	Registry  *Registry
}

// ConnectedTools 收集全部已连接服务器的桥接工具。
func (b *Bridge) ConnectedTools() []domain.Tool {
	var out []domain.Tool
	servers, err := b.Proxies.ListMcpServers()
	if err != nil {
		return out
	}
	for _, server := range servers {
		if !server.Enabled {
			continue
		}
		conn := b.Registry.Get(server.ID)
		if conn == nil {
			continue
		}
		tools, err := b.Proxies.ListMcpTools(server.ID)
		if err != nil {
			continue
		}
		for _, t := range tools {
			out = append(out, &bridgedTool{conn: conn, server: server.Name, tool: t})
		}
	}
	return out
}

type bridgedTool struct {
	conn   domain.McpConnection
	server string
	tool   domain.McpTool
}

func (t *bridgedTool) Name() string { return "mcp__" + t.server + "__" + t.tool.Name }
func (t *bridgedTool) Description() string {
	if t.tool.Description == "" {
		return "MCP 工具 " + t.server + "/" + t.tool.Name
	}
	return t.tool.Description
}
func (t *bridgedTool) ParametersSchemaJSON() string { return t.tool.SchemaJSON }

func (t *bridgedTool) Execute(argumentsJSON string, ctx domain.ToolContext) domain.ToolResult {
	out, err := t.conn.CallTool(t.tool.Name, argumentsJSON)
	if err != nil {
		return domain.ToolResult{Success: false, Output: "MCP 工具调用失败: " + err.Error()}
	}
	return domain.ToolResult{Success: true, Output: out}
}
