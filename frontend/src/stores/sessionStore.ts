import { defineStore } from 'pinia'
import { ref } from 'vue'
import { sessionsApi } from '@/api/sessions'
import { chatApi } from '@/api/chat'
import type { Session } from '@/types/domain'
import type { ChatMessage, ToolCallState } from '@/types/chat'
import type { ReasoningEffort } from '@/types/domain'

// 聊天中每条助手消息关联的工具调用状态（前端流式渲染用）
export interface StreamingAssistant {
  content: string
  toolCalls: ToolCallState[]
  turnIndex: number
}

export const useSessionStore = defineStore('session', () => {
  const sessions = ref<Session[]>([])
  const current = ref<Session | null>(null)
  const messages = ref<ChatMessage[]>([])
  const streaming = ref<StreamingAssistant | null>(null)
  const running = ref(false)
  const error = ref<string | null>(null)

  let abortController: AbortController | null = null

  async function loadSessions() {
    sessions.value = await sessionsApi.getAll()
  }

  async function select(id: string) {
    current.value = await sessionsApi.get(id)
    messages.value = await sessionsApi.messages(id)
    streaming.value = null
  }

  async function create(providerId: number, modelId: string, reasoningEffort: string, title?: string, workspacePath?: string | null, projectId?: number | null) {
    const session = await sessionsApi.create({ title, providerId, modelId, reasoningEffort, workspacePath, projectId })
    await loadSessions()
    current.value = session
    messages.value = []
    return session
  }

  async function update(id: string, data: Partial<{ title: string; providerId: number; modelId: string; reasoningEffort: string; workspacePath: string | null; status: string }>) {
    const session = await sessionsApi.update(id, data)
    await loadSessions()
    if (current.value?.id === id) current.value = session
    return session
  }

  async function remove(id: string) {
    await sessionsApi.remove(id)
    await loadSessions()
    if (current.value?.id === id) {
      current.value = null
      messages.value = []
    }
  }

  // 发送消息：SSE 流式接收，事件驱动渲染
  async function send(message: string, reasoningEffortOverride: ReasoningEffort | null) {
    const session = current.value
    if (!session || running.value) return

    error.value = null
    running.value = true
    streaming.value = { content: '', toolCalls: [], turnIndex: 0 }
    abortController = new AbortController()

    try {
      await chatApi.chat(session.id, message, reasoningEffortOverride, (eventName, data) => {
        const stream = streaming.value
        if (!stream) return
        switch (eventName) {
          case 'turn_started':
            stream.turnIndex = data.turnIndex ?? 0
            break
          case 'message_delta':
            stream.content += data.content ?? ''
            break
          case 'tool_call_started':
            stream.toolCalls.push({
              id: data.id,
              name: data.name,
              argumentsJson: data.arguments ? JSON.stringify(data.arguments, null, 2) : '{}',
              status: 'running',
            })
            break
          case 'tool_call_finished': {
            const call = stream.toolCalls.find((c) => c.id === data.id)
            if (call) {
              call.status = data.isError ? 'error' : 'finished'
              call.result = data.result
            }
            break
          }
          case 'error':
            error.value = data.message
            break
          case 'turn_completed':
          case 'cancelled':
            break
        }
      }, abortController.signal)
    } catch (e) {
      if (!abortController.signal.aborted) {
        error.value = e instanceof Error ? e.message : String(e)
      }
    } finally {
      streaming.value = null
      running.value = false
      abortController = null
      // 完成后拉取持久化消息刷新视图（失败不掩盖正常结束路径）
      try {
        if (current.value) {
          messages.value = await sessionsApi.messages(current.value.id)
        }
        await loadSessions()
      } catch {
        // 刷新失败保持现状
      }
    }
  }

  async function stop() {
    if (!current.value) return
    try {
      await chatApi.stop(current.value.id)
    } finally {
      abortController?.abort()
    }
  }

  return {
    sessions,
    current,
    messages,
    streaming,
    running,
    error,
    loadSessions,
    select,
    create,
    update,
    remove,
    send,
    stop,
  }
})
