// Package mcp：MCP 客户端（stdio 进程 JSON-RPC 2.0 / streamable HTTP）。
package mcp

import (
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"strings"
	"sync"
	"time"

	"codingagent/internal/domain"
)

const requestTimeout = 30 * time.Second

// ---- stdio 传输 ----

type StdioConnection struct {
	server domain.McpServer

	mu        sync.Mutex
	cmd       *exec.Cmd
	stdin     io.WriteCloser
	nextID    int
	pending   map[int64]chan string
	lifetime  context.CancelFunc
	connected bool
}

func NewStdio(server domain.McpServer) *StdioConnection {
	return &StdioConnection{server: server, pending: map[int64]chan string{}}
}

func (c *StdioConnection) ServerID() int64     { return c.server.ID }
func (c *StdioConnection) ServerName() string  { return c.server.Name }
func (c *StdioConnection) IsConnected() bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.connected
}

func (c *StdioConnection) Initialize() error {
	c.mu.Lock()
	if c.connected || c.server.Command == nil || *c.server.Command == "" {
		c.mu.Unlock()
		return nil
	}
	ctx, cancel := context.WithCancel(context.Background())
	c.lifetime = cancel

	cmd := exec.Command(*c.server.Command, decodeArgs(c.server.ArgsJSON)...)
	cmd.Env = append(os.Environ(), decodeEnv(c.server.EnvJSON)...)
	stdin, err := cmd.StdinPipe()
	if err != nil {
		c.mu.Unlock()
		cancel()
		return fmt.Errorf("打开 stdin 失败: %w", err)
	}
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		c.mu.Unlock()
		cancel()
		return fmt.Errorf("打开 stdout 失败: %w", err)
	}
	if err := cmd.Start(); err != nil {
		c.mu.Unlock()
		cancel()
		return fmt.Errorf("启动 MCP 进程失败: %w", err)
	}
	c.cmd = cmd
	c.stdin = stdin
	c.mu.Unlock()

	// 读循环使用连接自身生命周期 ctx（与调用方请求分离）
	go c.readLoop(stdout, ctx)

	// 初始化失败时清理子进程，避免泄漏
	if err := func() error {
		_, err := c.request(ctx, "initialize", map[string]any{
			"protocolVersion": "2024-11-05",
			"capabilities":    map[string]any{},
			"clientInfo":      map[string]any{"name": "CodingAgent", "version": "1.0.0"},
		})
		if err != nil {
			return err
		}
		return c.notify(ctx, "notifications/initialized")
	}(); err != nil {
		c.Close()
		return err
	}

	c.mu.Lock()
	c.connected = true
	c.mu.Unlock()
	return nil
}

func (c *StdioConnection) readLoop(stdout io.ReadCloser, ctx context.Context) {
	defer stdout.Close()
	reader := bufio.NewReader(stdout)
	for {
		line, err := reader.ReadString('\n')
		line = strings.TrimRight(line, "\r\n")
		if line != "" && strings.HasPrefix(strings.TrimSpace(line), "{") {
			c.handleLine(strings.TrimSpace(line))
		}
		if err != nil {
			return
		}
		if ctx.Err() != nil {
			return
		}
	}
}

func (c *StdioConnection) handleLine(line string) {
	var msg struct {
		ID    *int64 `json:"id"`
		Error *struct {
			Message string `json:"message"`
		} `json:"error"`
	}
	if err := json.Unmarshal([]byte(line), &msg); err != nil {
		return // 忽略非 JSON 行
	}
	if msg.ID == nil {
		return
	}
	c.mu.Lock()
	if ch, ok := c.pending[*msg.ID]; ok {
		ch <- line
	}
	c.mu.Unlock()
}

// request JSON-RPC 请求：持锁仅完成写 payload + 注册 pending，解锁后等待响应（避免与读循环互锁）。
func (c *StdioConnection) request(ctx context.Context, method string, params any) (json.RawMessage, error) {
	c.mu.Lock()
	if c.stdin == nil {
		c.mu.Unlock()
		return nil, fmt.Errorf("MCP 进程未启动")
	}

	c.nextID++
	id := int64(c.nextID)
	payload, _ := json.Marshal(map[string]any{"jsonrpc": "2.0", "id": id, "method": method, "params": params})
	if _, err := c.stdin.Write(append(payload, '\n')); err != nil {
		c.mu.Unlock()
		return nil, fmt.Errorf("写入 MCP 进程失败: %w", err)
	}

	ch := make(chan string, 1)
	c.pending[id] = ch
	c.mu.Unlock()

	// 解锁后等待：读循环 handleLine 可抢锁投递响应（channel 缓冲 1，响应迟到不阻塞读循环）
	cleanup := func() {
		c.mu.Lock()
		delete(c.pending, id)
		c.mu.Unlock()
	}
	deadline := time.NewTimer(requestTimeout)
	defer deadline.Stop()
	select {
	case line := <-ch:
		cleanup()
		var msg struct {
			Result json.RawMessage `json:"result"`
			Error  *struct {
				Message string `json:"message"`
			} `json:"error"`
		}
		if err := json.Unmarshal([]byte(line), &msg); err != nil {
			return nil, fmt.Errorf("解析 MCP 响应失败: %w", err)
		}
		if msg.Error != nil {
			return nil, fmt.Errorf("MCP 错误: %s", msg.Error.Message)
		}
		return msg.Result, nil
	case <-deadline.C:
		cleanup()
		return nil, fmt.Errorf("MCP 请求超时: %s", method)
	case <-ctx.Done():
		cleanup()
		return nil, fmt.Errorf("MCP 请求已取消")
	}
}

func (c *StdioConnection) notify(ctx context.Context, method string) error {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.stdin == nil {
		return fmt.Errorf("MCP 进程未启动")
	}
	payload, _ := json.Marshal(map[string]any{"jsonrpc": "2.0", "method": method})
	if _, err := c.stdin.Write(append(payload, '\n')); err != nil {
		return fmt.Errorf("写入 MCP 进程失败: %w", err)
	}
	return nil
}

func (c *StdioConnection) ListTools() ([]domain.McpToolInfo, error) {
	result, err := c.request(context.Background(), "tools/list", map[string]any{})
	if err != nil {
		return nil, err
	}
	return parseToolsResult(result)
}

func (c *StdioConnection) CallTool(toolName, argumentsJSON string) (string, error) {
	var args any
	if err := json.Unmarshal([]byte(orEmptyJSON(argumentsJSON)), &args); err != nil || args == nil {
		args = map[string]any{}
	}
	result, err := c.request(context.Background(), "tools/call", map[string]any{"name": toolName, "arguments": args})
	if err != nil {
		return "", err
	}
	return extractToolText(result)
}

func (c *StdioConnection) Close() {
	c.mu.Lock()
	connected := c.connected
	c.connected = false
	cancel := c.lifetime
	stdin := c.stdin
	cmd := c.cmd
	c.lifetime = nil
	c.stdin = nil
	c.mu.Unlock()

	if cancel != nil {
		cancel()
	}
	if stdin != nil {
		stdin.Close()
	}
	if cmd != nil && cmd.Process != nil {
		_ = cmd.Process.Kill()
		go cmd.Wait()
	}
	_ = connected
}

// ---- streamable HTTP 传输 ----

type HTTPConnection struct {
	server domain.McpServer
	client *http.Client

	mu        sync.Mutex
	nextID    int
	connected bool
}

func NewHTTP(server domain.McpServer) *HTTPConnection {
	return &HTTPConnection{
		server: server,
		client: &http.Client{Timeout: requestTimeout},
	}
}

func (c *HTTPConnection) ServerID() int64    { return c.server.ID }
func (c *HTTPConnection) ServerName() string { return c.server.Name }
func (c *HTTPConnection) IsConnected() bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.connected
}

func (c *HTTPConnection) Initialize() error {
	c.mu.Lock()
	if c.connected || c.server.URL == nil || *c.server.URL == "" {
		c.mu.Unlock()
		return nil
	}
	c.mu.Unlock()

	if _, err := c.request("initialize", map[string]any{
		"protocolVersion": "2024-11-05",
		"capabilities":    map[string]any{},
		"clientInfo":      map[string]any{"name": "CodingAgent", "version": "1.0.0"},
	}); err != nil {
		return err
	}
	if err := c.notify("notifications/initialized"); err != nil {
		return err
	}
	c.mu.Lock()
	c.connected = true
	c.mu.Unlock()
	return nil
}

func (c *HTTPConnection) post(payload string) (string, error) {
	req, err := http.NewRequest(http.MethodPost, *c.server.URL, bytes.NewReader([]byte(payload)))
	if err != nil {
		return "", err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Accept", "application/json, text/event-stream")
	for k, v := range decodeHeaders(c.server.HeadersJSON) {
		req.Header.Set(k, v)
	}
	resp, err := c.client.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", err
	}
	if resp.StatusCode != http.StatusOK && resp.StatusCode != http.StatusAccepted {
		return "", fmt.Errorf("MCP HTTP 返回 %d: %s", resp.StatusCode, truncateStr(string(body), 300))
	}
	contentType := resp.Header.Get("Content-Type")
	if strings.Contains(contentType, "event-stream") {
		// SSE 响应：取最后一个 data: 行
		last := ""
		for _, line := range strings.Split(string(body), "\n") {
			line = strings.TrimSpace(line)
			if strings.HasPrefix(line, "data:") {
				last = strings.TrimSpace(line[len("data:"):])
			}
		}
		if last == "" {
			return "{}", nil
		}
		return last, nil
	}
	if len(body) == 0 {
		return "{}", nil
	}
	return string(body), nil
}

func (c *HTTPConnection) request(method string, params any) (json.RawMessage, error) {
	c.mu.Lock()
	c.nextID++
	id := int64(c.nextID)
	c.mu.Unlock()

	payload, _ := json.Marshal(map[string]any{"jsonrpc": "2.0", "id": id, "method": method, "params": params})
	body, err := c.post(string(payload))
	if err != nil {
		return nil, err
	}
	var msg struct {
		Result json.RawMessage `json:"result"`
		Error  *struct {
			Message string `json:"message"`
		} `json:"error"`
	}
	if err := json.Unmarshal([]byte(body), &msg); err != nil {
		return nil, fmt.Errorf("解析 MCP 响应失败: %w", err)
	}
	if msg.Error != nil {
		return nil, fmt.Errorf("MCP 错误: %s", msg.Error.Message)
	}
	return msg.Result, nil
}

func (c *HTTPConnection) notify(method string) error {
	payload, _ := json.Marshal(map[string]any{"jsonrpc": "2.0", "method": method})
	_, err := c.post(string(payload))
	return err
}

func (c *HTTPConnection) ListTools() ([]domain.McpToolInfo, error) {
	result, err := c.request("tools/list", map[string]any{})
	if err != nil {
		return nil, err
	}
	return parseToolsResult(result)
}

func (c *HTTPConnection) CallTool(toolName, argumentsJSON string) (string, error) {
	var args any
	if err := json.Unmarshal([]byte(orEmptyJSON(argumentsJSON)), &args); err != nil || args == nil {
		args = map[string]any{}
	}
	result, err := c.request("tools/call", map[string]any{"name": toolName, "arguments": args})
	if err != nil {
		return "", err
	}
	return extractToolText(result)
}

func (c *HTTPConnection) Close() {
	c.mu.Lock()
	c.connected = false
	c.mu.Unlock()
}

// ---- 辅助 ----

func parseToolsResult(result json.RawMessage) ([]domain.McpToolInfo, error) {
	var parsed struct {
		Tools []domain.McpToolInfo `json:"tools"`
	}
	if err := json.Unmarshal(result, &parsed); err != nil {
		return nil, fmt.Errorf("解析 tools/list 失败: %w", err)
	}
	for i := range parsed.Tools {
		if parsed.Tools[i].SchemaJSON == "" {
			parsed.Tools[i].SchemaJSON = "{}"
		}
	}
	return parsed.Tools, nil
}

func extractToolText(result json.RawMessage) (string, error) {
	var parsed struct {
		IsError bool `json:"isError"`
		Content []struct {
			Type string `json:"type"`
			Text string `json:"text"`
		} `json:"content"`
	}
	if err := json.Unmarshal(result, &parsed); err != nil {
		return string(result), nil // 非 content 结构时返回原文
	}
	var sb strings.Builder
	for _, c := range parsed.Content {
		if c.Type == "text" {
			sb.WriteString(c.Text)
			sb.WriteString("\n")
		}
	}
	out := strings.TrimRight(sb.String(), "\n")
	if out == "" {
		return string(result), nil
	}
	if parsed.IsError {
		return "工具执行错误: " + out, nil
	}
	return out, nil
}

func decodeArgs(jsonPtr *string) []string {
	if jsonPtr == nil {
		return nil
	}
	var args []string
	if err := json.Unmarshal([]byte(*jsonPtr), &args); err != nil {
		return nil
	}
	return args
}

func decodeEnv(jsonPtr *string) []string {
	if jsonPtr == nil {
		return nil
	}
	var env map[string]string
	if err := json.Unmarshal([]byte(*jsonPtr), &env); err != nil || env == nil {
		return nil
	}
	var out []string
	for k, v := range env {
		out = append(out, k+"="+v)
	}
	return out
}

func decodeHeaders(jsonPtr *string) map[string]string {
	if jsonPtr == nil {
		return nil
	}
	var headers map[string]string
	if err := json.Unmarshal([]byte(*jsonPtr), &headers); err != nil || headers == nil {
		return nil
	}
	return headers
}

func orEmptyJSON(s string) string {
	if strings.TrimSpace(s) == "" {
		return "{}"
	}
	return s
}

func truncateStr(s string, max int) string {
	if len(s) <= max {
		return s
	}
	return s[:max]
}
