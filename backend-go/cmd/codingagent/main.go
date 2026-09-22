// CodingAgent Go 后端入口：显式装配（无 DI 容器），数据目录可用 CODINGAGENT_DATA_DIR 覆盖。
package main

import (
	"log"
	"mime"
	"net/http"
	"os"
	"path/filepath"

	"codingagent/internal/agent"
	"codingagent/internal/api"
	"codingagent/internal/llm"
	"codingagent/internal/mcp"
	"codingagent/internal/security"
	"codingagent/internal/store"
)

func main() {
	// PWA：manifest MIME（Go 内置表缺 .webmanifest）
	mime.AddExtensionType(".webmanifest", "application/manifest+webmanifest")
	mime.AddExtensionType(".js", "text/javascript")

	addr := os.Getenv("CODINGAGENT_ADDR")
	if addr == "" {
		addr = "http://localhost:8080"
	}
	dataDir := os.Getenv("CODINGAGENT_DATA_DIR")
	if dataDir == "" {
		dataDir = "data"
	}
	if err := os.MkdirAll(dataDir, 0o755); err != nil {
		log.Fatalf("创建数据目录失败: %v", err)
	}
	defaultWorkspace := filepath.Join(dataDir, "workspace")
	if err := os.MkdirAll(defaultWorkspace, 0o755); err != nil {
		log.Fatalf("创建工作区失败: %v", err)
	}

	db, err := store.Open(filepath.Join(dataDir, "codingagent.db"))
	if err != nil {
		log.Fatalf("打开数据库失败: %v", err)
	}
	defer db.Close()

	// JWT 密钥首次启动随机生成并持久化（幂等）
	jwtSecret, err := db.GetSetting("jwt_secret")
	if err != nil {
		log.Fatalf("读取设置失败: %v", err)
	}
	if jwtSecret == "" {
		jwtSecret, err = security.GenerateSecret()
		if err != nil {
			log.Fatalf("生成密钥失败: %v", err)
		}
	}
	if err := db.SeedDefault(jwtSecret); err != nil {
		log.Fatalf("种子数据失败: %v", err)
	}

	tokens, err := security.NewTokenService(jwtSecret)
	if err != nil {
		log.Fatalf("初始化令牌服务失败: %v", err)
	}

	// 显式装配：store → llm/agent → api（无容器，依赖在构造期可见）
	proxyDB := db
	bridge := &mcp.Bridge{Proxies: proxyDB, Registry: mcp.NewRegistry()}
	registry := &agent.Registry{Proxies: proxyDB, Bridge: bridge}
	llmFactory := llm.NewFactory(proxyDB)
	agentLoop := &agent.Loop{
		Proxies:     proxyDB,
		LLM:         llmFactory,
		Registry:    registry,
		Context:     agent.ContextWindowManager{},
		BuildPrompt: func(workspacePath string) string {
			return agent.SystemPromptBuilder{
				Proxies:      agent.PromptStoreAdapter{Proxies: proxyDB},
				McpToolNames: registry.McpToolNames,
			}.Build(workspacePath)
		},
	}

	// task 执行器（运行时注入，避免构造期循环）
	runner := &agent.SubAgentRunner{Proxies: proxyDB, LLM: agentLoop.LLM, Loop: agentLoop}
	registry.TaskRunner = func(subagent, input, workspacePath string) string {
		out, err := runner.Run(subagent, input, workspacePath)
		if err != nil {
			return "[子代理错误] " + err.Error()
		}
		return out
	}

	// 生产托管前端 dist（../frontend/dist 存在时）
	frontendDist := "../frontend/dist"
	if _, err := os.Stat(frontendDist + "/index.html"); err != nil {
		frontendDist = ""
	}

	server := &api.Server{
		Proxies:       proxyDB,
		Loop:          agentLoop,
		Tokens:        tokens,
		WorkspaceRoot: defaultWorkspace,
		FrontendDist:  frontendDist,
	}

	host := httpAddr(addr)
	log.Printf("CodingAgent(Go) 启动：监听 %s，数据目录 %s，默认工作区 %s", host, dataDir, defaultWorkspace)
	if err := http.ListenAndServe(host, server.Routes()); err != nil {
		log.Fatalf("服务器退出: %v", err)
	}
}

// httpAddr 把 "http://localhost:8080" 归一为 ListenAndServe 地址（"host:port"）。
func httpAddr(addr string) string {
	for _, prefix := range []string{"http://", "https://"} {
		if len(addr) >= len(prefix) && addr[:len(prefix)] == prefix {
			addr = addr[len(prefix):]
			break
		}
	}
	return addr
}
