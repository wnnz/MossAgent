import type { MessageRole } from './domain'

// 聊天消息与 SSE 事件负载类型
export interface ToolCall {
  id: string
  name: string
  argumentsJson: string
}

export interface ToolCallState extends ToolCall {
  status: 'running' | 'finished' | 'error'
  result?: string
}

export interface ChatMessage {
  id: number
  sessionId: string
  role: MessageRole
  content: string
  toolCallsJson?: string | null
  toolCallId?: string | null
  name?: string | null
  createdAt: string
}

export interface TurnStartedData {
  turnIndex: number
}
export interface MessageDeltaData {
  content: string
}
export interface ToolCallEventData {
  id: string
  name: string
  arguments?: Record<string, unknown>
  result?: string
  isError?: boolean
}
export interface ErrorData {
  message: string
}
