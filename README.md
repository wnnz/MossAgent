# Coding Agent

本地优先的单机 Coding Agent Web 应用：.NET 10 + Vue 3 + shadcn 风格 UI。支持 AI 提供商/模型管理、网络代理、Agent 工具循环（文件/命令/记忆/技能/MCP/子代理）、SSE 流式输出与 Codex 风格界面。

## 架构

- **后端** `backend/`：ASP.NET Core（.NET 10）Web API，SSE 流式输出，SQLite（EF Core，EnsureCreated 自动建库）
  - `src/CodingAgent.Domain`：领域模型与接口（一文件一类）
  - `src/CodingAgent.Infrastructure`：EF Core 仓储、OpenAI 兼容/Anthropic LLM 客户端、MCP 客户端（stdio/HTTP）、PBKDF2 + JWT Cookie、工作区沙箱、内置工具
  - `src/CodingAgent.Agent`：AgentLoop、系统提示词构建、上下文窗口管理、子代理执行器
  - `src/CodingAgent.Api`：控制器、SSE 写出、中间件
  - `tests/CodingAgent.Tests`：xUnit 测试
- **前端** `frontend/`：Vue 3 + Vite + TypeScript + Tailwind CSS 4 + Pinia + Vue Router（`ui/` 组件为手写 shadcn 风格，同命名同 CSS 变量，未用 CLI 生成）

## 启动（开发）

```bash
# 1. 后端（默认 http://localhost:8080，数据目录 backend/data/）
dotnet run --project backend/src/CodingAgent.Api

# 2. 前端（http://localhost:5173，/api 代理到 8080）
cd frontend
npm install
npm run dev
```

首次访问自动进入设密码页（仅允许设置一次），成功即种 httpOnly JWT Cookie 登录。

## 生产构建（单端口）

```bash
cd frontend && npm run build   # 产物 frontend/dist/
dotnet run --project backend/src/CodingAgent.Api
# 访问 http://localhost:8080 —— 后端自动托管 frontend/dist 并做 SPA fallback
```

## 数据目录与工作区

- 应用数据（SQLite）：默认 `backend/data/`，可用环境变量 `CODINGAGENT_DATA_DIR` 覆盖
- Agent 工作区：默认 `backend/data/workspace/`，可在设置页「工作区」修改（settings.workspace_path）
- 文件工具与命令执行被限制在工作区内（拒绝路径穿越）；`run_command` 自动执行（默认超时 120s、输出上限截断）

## 安全说明

- 密码 PBKDF2-SHA256（100k 迭代）加盐哈希；会话为 httpOnly JWT Cookie（7/30 天）
- **API Key 明文存于本地 SQLite**（local-first 单机场景，后续可加 DPAPI 加密）
- MCP stdio 服务器会以配置的命令启动本地子进程，仅添加可信来源

## 架构

- **后端（Go）** `backend-go/`：Go 1.22 net/http（Go 1.22 method+path 路由）+ modernc.org/sqlite（纯驱动无 CGO）+ golang.org/x/net（socks5），显式装配无 DI 容器
  - `cmd/codingagent`：入口（显式装配）
  - `internal/domain`：实体与字符串枚举（零转换）
  - `internal/store`：SQLite 存储
  - `internal/security`：PBKDF2 + HS256 JWT（零依赖）
  - `internal/workspace`：文件工具沙箱
  - `internal/proxy`：http/socks5 客户端工厂
  - `internal/llm`：OpenAI 兼容 + Anthropic SSE 流式（行式增量解析）
  - `internal/mcp`：stdio / HTTP JSON-RPC 客户端 + 注册表 + 工具桥接
  - `internal/tools`：11 个内置工具
  - `internal/agent`：AgentLoop、上下文裁剪、子代理执行器
  - `internal/api`：全部端点 + Cookie 认证门禁 + chat SSE（owner 语义）
- **后端（Go）** `backend-go/`：Go 1.22 + modernc.org/sqlite 纯驱动，API/SSE/DTO 契约与下方描述完全一致（前端零改动）
  - `internal/domain`：实体 + 字符串枚举
  - `internal/store`：SQLite 存储
  - `internal/security`：PBKDF2 + JWT（零依赖）
  - `internal/workspace`：沙箱
  - `internal/proxy` / `internal/llm` / `internal/mcp` / `internal/tools` / `internal/agent` / `internal/api`
- **前端** `frontend/`：Vue 3 + Vite + TypeScript + Tailwind CSS 4 + Pinia + Vue Router（`ui/` 组件为手写 shadcn 风格，同命名同 CSS 变量，未用 CLI 生成）

## 启动（开发）

```bash
# 1. Go 后端（默认 localhost:8080，数据目录 backend-go/data/，可用 CODINGAGENT_DATA_DIR 覆盖）
cd backend-go
go run ./cmd/codingagent

# 2. 前端（http://localhost:5173，/api 代理到 8080）
cd frontend
npm install
npm run dev
```

首次访问自动进入设密码页（仅允许设置一次），成功即种 httpOnly JWT Cookie 登录。

## 项目（工作区分组）

会话归属项目，支持创建或导入：
- **创建**：仅填名称（目录自动建立在默认工作区下）；**导入**：粘贴已有目录路径
- 侧栏「项目」分组：项目 → 会话嵌套展示（缩进）；空态提示「你想让我们在 {项目} 中构建什么？」
- Composer 上下文条显示 项目名 | 本地 | git 分支（`GET /api/projects/{id}/branch`，失败回退 main）
- 会话工作区=项目路径（文件工具与命令沙箱=项目目录）；无项目时用默认工作区
- API：`GET/POST /api/projects`、`DELETE /api/projects/{id}`、`GET /api/projects/{id}/branch`

## PWA

会话归属项目，支持创建或导入：
- **创建**：仅填名称（目录自动建立在默认工作区下）；**导入**：粘贴已有目录路径
- 侧栏「项目」分组：项目 → 会话嵌套展示（缩进）；空态提示「你想让我们在 {项目} 中构建什么？」
- Composer 上下文条显示 项目名 | 本地 | git 分支（`GET /api/projects/{id}/branch`，失败回退 main）
- 会话工作区=项目路径（文件工具与命令沙箱=项目目录）；无项目时用默认工作区
- API：`GET/POST /api/projects`、`DELETE /api/projects/{id}`、`GET /api/projects/{id}/branch`


前端为可安装 PWA（`vite-plugin-pwa`，autoUpdate）：
- 应用外壳预缓存（19 项 js/css/html/svg/png），离线可打开；`/api` 请求不被 SW 拦截（chat SSE 不受影响）
- `manifest.webmanifest` + 图标（192/512 + maskable）+ `theme-color`
- Chrome 打开 http://localhost:8080 后地址栏可「安装应用」，`sw.js` 自动更新

## 生产构建（单端口）

```bash
cd frontend && npm run build   # 产物 frontend/dist/
cd backend-go && go build -o codingagent.exe ./cmd/codingagent
./codingagent.exe             # 访问 http://localhost:8080 —— 自动托管 ../frontend/dist 并做 SPA fallback
```

## 测试

```bash
cd backend-go
go test ./...    # security(10) / workspace(6) / llm(8)
go vet ./...
cd frontend && npm run build   # vue-tsc --noEmit + vite build
```
