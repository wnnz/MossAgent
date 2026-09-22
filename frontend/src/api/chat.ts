import { ssePost } from '@/lib/sse'
import type { ReasoningEffort } from '@/types/domain'

export const chatApi = {
  // SSE 流式聊天；返回 Promise 由调用方 await；abort 由调用方传 AbortSignal
  chat: (
    sessionId: string,
    message: string,
    reasoningEffort: ReasoningEffort | null,
    onEvent: (eventName: string, data: any) => void,
    signal?: AbortSignal,
  ) =>
    ssePost(
      `/api/sessions/${sessionId}/chat`,
      { message, reasoningEffort: reasoningEffort ?? undefined },
      { onEvent },
      signal,
    ),
  stop: (sessionId: string) => httpPostStop(sessionId),
}

async function httpPostStop(sessionId: string): Promise<{ cancelled: boolean }> {
  const response = await fetch(`/api/sessions/${sessionId}/stop`, {
    method: 'POST',
    credentials: 'include',
  })
  return (await response.json()) as { cancelled: boolean }
}
