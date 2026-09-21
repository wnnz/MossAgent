# Coding Agent 实现计划（.NET 10 + Vue 3 + shadcn-vue + Tailwind）

## 一、总体架构

- **后端**：ASP.NET Core（.NET 10）Web API，SSE 流式输出，SQLite（EF Core）存储
- **前端**：Vue 3 + Vite + TypeScript + shadcn-vue + Tailwind CSS 4 + Pinia + Vue Router
- **部署**：开发时 Vite 代理 `/api` 到后端；生产时 `npm run build` 产物由 ASP.NET 托管（单端口）
- **约束落实**：每个 `.cs` 文件只含一个 class/record；任何文件保持小体量，按职责拆分

## 二、代码结构

```
backend/
  CodingAgent.sln
  src/
    CodingAgent.Domain/          # 纯领域模型与接口（POCO，一文件一类）
      Entities/    Session.cs ChatMessage.cs Provider.cs ProviderModel.cs ProxyServer.cs
                   SubAgent.cs MemoryItem.cs Skill.cs McpServer.cs McpTool.cs AppSetting.cs AuthRecord.cs
      Enums/       ProviderType.cs ProxyScheme.cs MessageRole.cs McpTransport.cs ReasoningEffort.cs MemoryScope.cs
      Interfaces/  ISessionRepository.cs IProviderRepository.cs ILlmClient.cs ITool.cs
                   IMcpConnectionFactory.cs IPasswordHasher.cs ITokenService.cs IWorkspaceGuard.cs ...
      Models/      LlmRequest.cs LlmStreamEvent.cs ToolCallSpec.cs AgentEvent.cs LlmToolDefinition.cs ...
    CodingAgent.Infrastructure/
      Database/    AppDbContext.cs DbSeeder.cs
      Repositories/ SessionRepository.cs ProviderRepository.cs ...（每实体一个）
      Llm/         OpenAiCompatibleClient.cs AnthropicClient.cs LlmClientFactory.cs ModelCatalogClient.cs
      Http/        ProxyHttpClientFactory.cs（http / socks5）
      Mcp/         McpConnection.cs StdioTransport.cs HttpTransport.cs McpToolBridge.cs
      Security/    Pbkdf2PasswordHasher.cs JwtCookieTokenService.cs
      Storage/     WorkspaceLocator.cs WorkspaceGuard.cs
      Tools/       ToolRegistry.cs tools/ReadFileTool.cs WriteFileTool.cs EditFileTool.cs
                   ListDirTool.cs GlobTool.cs GrepTool.cs RunCommandTool.cs
                   MemorySaveTool.cs MemorySearchTool.cs SkillLoadTool.cs TaskTool.cs
    CodingAgent.Agent/
      AgentLoop.cs SystemPromptBuilder.cs ContextWindowManager.cs SubAgentRunner.cs
    CodingAgent.Api/
      Program.cs
      Controllers/ AuthController.cs ProvidersController.cs ProxiesController.cs SessionsController.cs
                   AgentChatController.cs SubAgentsController.cs MemoriesController.cs
                   SkillsController.cs McpController.cs SettingsController.cs
      Sse/         SseEventWriter.cs SessionCancellationMap.cs
      Contracts/   按域拆分的 DTO（每个 record 一个文件）
      Middleware/  CookieAuthMiddleware.cs ExceptionMiddleware.cs
  tests/CodingAgent.Tests/  PasswordHasherTests.cs WorkspaceGuardTests.cs AgentLoopTests.cs
                            ContextWindowManagerTests.cs ProxyFactoryTests.cs

frontend/
  src/
    main.ts App.vue router/index.ts
    lib/    http.ts utils.ts(cn) theme.ts sse.ts
    api/    auth.ts providers.ts proxies.ts sessions.ts chat.ts subagents.ts memories.ts skills.ts mcp.ts settings.ts
    stores/ authStore.ts themeStore.ts sessionStore.ts settingsStore.ts
    types/  domain.ts chat.ts
    views/  SetupView.vue LoginView.vue ChatView.vue SettingsView.vue
    components/
      layout/   AppSidebar.vue TopBar.vue
      chat/     MessageList.vue UserMessage.vue AssistantMessage.vue ToolCallCard.vue
                Composer.vue ModelPicker.vue ReasoningPicker.vue
      settings/ SecurityPanel.vue WorkspacePanel.vue ProvidersPanel.vue ProviderDialog.vue
                ModelDialog.vue ProxiesPanel.vue ProxyDialog.vue SubAgentsPanel.vue SubAgentDialog.vue
                MemoriesPanel.vue SkillsPanel.vue McpPanel.vue McpServerDialog.vue AppearancePanel.vue
      ui/       shadcn-vue 生成组件（button/input/select/dialog/card/tabs/switch/badge/
                scroll-area/dropdown-menu/tooltip/separator/sonner/label/textarea）
```

## 三、数据模型（SQLite，EF Core）

| 表 | 关键字段 |
|---|---|
| auth_record | password_hash(PBKDF2), salt, created_at（单行） |
| providers | name, type(openai_compatible/anthropic), base_url, api_key, proxy_id?, enabled |
| provider_models | provider_id, model_id, display_name, supports_tools, supports_reasoning, default_reasoning_effort, max_context_tokens, max_output_tokens, is_custom |
| proxies | name, scheme(http/socks5), host, port, username, password, enabled |
| sessions | title, provider_id, model_id, reasoning_effort, workspace_path?, status, created/updated_at |
| messages | session_id, role, content, tool_calls_json?, tool_call_id?, name?, created_at |
| sub_agents | name, description, invocation_rule(何时调用的自然语言规则), system_prompt, provider_id, model_id, reasoning_effort, max_turns, allowed_tools_json, enabled |
| memories | scope(global/project), title, content, tags, updated_at |
| skills | name, description, instructions(markdown), enabled |
| mcp_servers | name, transport(stdio/http), command, args_json, env_json, url, headers_json, enabled, auto_connect |
| mcp_tools | server_id, name, description, schema_json（tools/list 缓存） |
| settings | key/value（工作区目录、默认模型、最大轮次等） |

数据目录默认 `backend/data/`（可用环境变量 `CODINGAGENT_DATA_DIR` 覆盖），应用数据与 agent 工作区分离。

## 四、各需求实现要点

1. **首次访问设密码 + 自动登录**
   - `GET /api/auth/status` 无需认证：返回 `{needsSetup, authenticated}`。
   - 未设置密码 → 前端 SetupView 创建密码（仅允许一次），成功即种 httpOnly JWT Cookie 登录。
   - Cookie 有效期 7/30 天（记住我），刷新/重访时路由守卫调 status，有效则直接进主界面；`PUT /api/auth/password` 改密，`POST /api/auth/logout` 注销。

2. **Codex 风格布局**
   - 左侧栏：New Task 按钮、会话列表、底部主题切换 + 设置入口；主区：顶栏（会话标题、模型选择器）+ 消息流 + 底部 Composer。
   - 用户消息气泡、助手消息 Markdown 全宽渲染、工具调用渲染为可折叠卡片（工具名 + 参数 + 输出），与 Codex 的 terminal 风格一致。

3. **主题切换**：light / dark / system 三态，Pinia + localStorage 持久化，`dark` class + shadcn-vue CSS 变量；system 跟随 `prefers-color-scheme`。

4. **AI 提供商 + 模型**
   - 支持 `openai_compatible`（OpenAI/DeepSeek/Qwen/OpenRouter/Ollama 等通用 `/v1/chat/completions`）与 `anthropic` 两种协议。
   - `POST /api/providers/{id}/models/refresh`：调用远端 `GET /v1/models` 拉取模型列表并 upsert；UI 也可手动添加模型（is_custom）。
   - 模型参数：工具调用/推理支持开关、默认思考强度（off/low/medium/high）、上下文长度 maxContextTokens、maxOutputTokens；Composer 可临时覆盖思考强度。

5. **网络代理**
   - 多个代理 CRUD（http / socks5，含认证），`ProxyHttpClientFactory` 按 provider 所选代理构建 HttpClient（SOCKS5 用 .NET 内建支持）。
   - `POST /api/proxies/{id}/test` 连通性测试；所有 LLM/模型列表请求经该工厂发出。

6. **Coding Agent 基础能力**
   - **Agent 循环**：构建系统提示词（基础人设 + 工作区摘要 + 记忆 + 技能索引 + MCP 工具 + 子代理及调用规则）→ 流式调用 LLM → 解析 tool_calls → 工具注册表顺序执行 → 结果回填继续，直至无工具调用或达到最大轮次（默认 25）。
   - **内置工具**：read_file / write_file / edit_file / list_dir / glob / grep / run_command（超时 + 输出上限）/ memory_save / memory_search / skill_load / task(子代理) + MCP 桥接工具（`mcp__<server>__<tool>`）。
   - **沙箱**：WorkspaceGuard 将文件工具限制在工作区目录内（拒绝路径穿越）；run_command 限时限量。
   - **上下文管理**：ContextWindowManager 按 maxContextTokens 估算 token，滑动窗口裁剪旧消息、超长工具输出截断为占位摘要。
   - **记忆管理**：global/project 两级记忆 CRUD + 检索，系统提示词注入摘要；UI 面板管理。
   - **技能管理**：Markdown 技能（名称/描述/正文/启用），系统提示词列出技能索引，`skill_load` 按名加载正文；UI 面板管理。
   - **MCP 管理**：stdio（进程 JSON-RPC 2.0）与 streamable HTTP 两种传输；initialize → tools/list → tools/call 最小客户端；UI 面板：服务器 CRUD、连接/断开、工具列表、测试调用。

7. **子代理**
   - `task` 工具触发 SubAgentRunner：独立 AgentLoop，使用子代理配置的提供商/模型/思考强度/最大轮次/允许工具（禁 task 防递归），返回最终文本作为工具结果。
   - 调用规则：每个子代理配置"何时调用"自然语言规则 + 描述，注入主代理系统提示词与 task 工具描述，由模型按规则决策调用。
   - UI：SubAgentsPanel + Dialog（规则、系统提示词、模型、思考强度、最大轮次、启用开关）。

8. **流式与停止**
   - `POST /api/sessions/{id}/chat` 返回 SSE：`turn_started / message_delta / tool_call_started / tool_call_finished / turn_completed / cancelled / error`。
   - `POST /api/sessions/{id}/stop` 取消对应 CancellationTokenSource，前端 AbortController 同步中断。

## 五、API 一览

```
auth:      GET /api/auth/status | POST setup | POST login | POST logout | PUT password
providers: CRUD /api/providers | POST {id}/models/refresh | POST {id}/models | PUT/DELETE {id}/models/{mid}
proxies:   CRUD /api/proxies | POST {id}/test
sessions:  GET/POST /api/sessions | PATCH/DELETE {id} | GET {id}/messages
agent:     POST /api/sessions/{id}/chat (SSE) | POST {id}/stop
subagents: CRUD /api/subagents
memories:  CRUD /api/memories | GET /api/memories/search?q=
skills:    CRUD /api/skills | PATCH {id}/toggle
mcp:       CRUD /api/mcp/servers | POST {id}/connect | POST {id}/disconnect | GET {id}/tools | POST {id}/tools/{tool}/call
settings:  GET/PUT /api/settings
```

## 六、实施阶段（每阶段可独立验证）

1. **脚手架**：解决方案 + 4 个 src 项目 + 测试项目；Vite + shadcn-vue + Tailwind 初始化；路由/守卫/主题/HTTP 封装；Codex 风格布局骨架（空数据）
2. **认证**：PBKDF2 + JWT Cookie、status/setup/login/logout/改密、SetupView/LoginView、守卫与自动登录
3. **设置基座**：工作区目录、提供商 CRUD、代理 CRUD + 连通测试、模型列表拉取/手动添加、UI 面板
4. **Agent 核心**：会话 CRUD、AgentLoop、OpenAI 兼容客户端、SSE 流式、聊天 UI（Markdown + 工具卡片）、停止、上下文窗口管理、内置文件/命令工具
5. **Anthropic 协议 + 子代理**：AnthropicClient、TaskTool、SubAgentRunner、调用规则注入、子代理 UI
6. **记忆与技能**：存储 + 工具 + 系统提示词注入 + 管理面板
7. **MCP**：stdio/HTTP 传输客户端、工具桥接到注册表、管理面板
8. **收尾**：错误/空态/Toast、错误中间件、xUnit 测试、`vue-tsc` 类型检查、README（启动/构建/数据目录说明）

## 七、验证方式

- `dotnet build` / `dotnet test`：密码哈希、路径沙箱（穿越用例）、FakeLlmClient 驱动的 AgentLoop 工具循环、上下文裁剪、代理工厂
- `npm run build` + `vue-tsc --noEmit`：前端类型与构建通过
- 手动 E2E 清单：设密码→刷新自动登录→添加提供商（拉模型）→添加代理并测试→流式聊天+工具调用演示→记忆保存/技能加载→本地 stdio MCP 服务器工具调用→子代理配置并被调用→主题三态切换→登出

## 八、假设（已确认按此实施）

- "代理"按**网络代理**（HTTP/SOCKS5）理解，供 AI 提供商请求走代理；子代理为独立功能
- API Key 明文存于本地 SQLite（local-first 单机场景），README 中说明；后续可加 DPAPI 加密
- run_command 自动执行（限时限输出），暂不做逐条审批，审批模式列为后续增强
- MCP v1 支持 stdio 与 streamable HTTP，SSE 传输列为后续增强
