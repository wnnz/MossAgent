// 领域类型（与后端 camelCase + lower_snake_case 枚举契约对齐）
export type ReasoningEffort = 'off' | 'low' | 'medium' | 'high'
export type ProviderType = 'openai_compatible' | 'anthropic'
export type ProxyScheme = 'http' | 'socks5'
export type McpTransport = 'stdio' | 'http'
export type MemoryScope = 'global' | 'project'
export type MessageRole = 'user' | 'assistant' | 'tool' | 'system'

export interface ProviderModel {
  id: number
  providerId: number
  modelId: string
  displayName: string
  supportsTools: boolean
  supportsReasoning: boolean
  defaultReasoningEffort: ReasoningEffort
  maxContextTokens: number
  maxOutputTokens: number
  isCustom: boolean
}

export interface Provider {
  id: number
  name: string
  type: ProviderType
  baseUrl: string
  apiKey: string
  proxyId: number | null
  enabled: boolean
  models: ProviderModel[]
}

export interface Proxy {
  id: number
  name: string
  scheme: ProxyScheme
  host: string
  port: number
  username: string | null
  password: string | null
  enabled: boolean
}

export interface Session {
  id: string
  title: string
  projectId: number | null
  providerId: number
  modelId: string
  reasoningEffort: ReasoningEffort
  workspacePath: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export interface SubAgent {
  id: number
  name: string
  description: string
  invocationRule: string
  systemPrompt: string
  providerId: number
  modelId: string
  reasoningEffort: ReasoningEffort
  maxTurns: number
  allowedToolsJson: string
  enabled: boolean
}

export interface MemoryItem {
  id: number
  scope: MemoryScope
  title: string
  content: string
  tags: string | null
  updatedAt: string
}

export interface Skill {
  id: number
  name: string
  description: string
  instructions: string
  enabled: boolean
}

export interface McpServer {
  id: number
  name: string
  transport: McpTransport
  command: string | null
  argsJson: string | null
  envJson: string | null
  url: string | null
  headersJson: string | null
  enabled: boolean
  autoConnect: boolean
}

export interface McpTool {
  id: number
  serverId: number
  name: string
  description: string
  schemaJson: string
}

export interface AppSetting {
  key: string
  value: string
}

export interface AuthStatus {
  needsSetup: boolean
  authenticated: boolean
}
