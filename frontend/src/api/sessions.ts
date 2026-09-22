import { http } from '@/lib/http'
import type { Session } from '@/types/domain'
import type { ChatMessage } from '@/types/chat'

export const sessionsApi = {
  getAll: (includeArchived = false) => http.get<Session[]>(`/api/sessions?includeArchived=${includeArchived}`),
  get: (id: string) => http.get<Session>(`/api/sessions/${id}`),
  create: (data: { title?: string; providerId: number; modelId: string; reasoningEffort: string; workspacePath?: string | null; projectId?: number | null }) =>
    http.post<Session>('/api/sessions', data),
  update: (id: string, data: Partial<{ title: string; providerId: number; modelId: string; reasoningEffort: string; workspacePath: string | null; status: string }>) =>
    http.patch<Session>(`/api/sessions/${id}`, data),
  remove: (id: string) => http.delete<void>(`/api/sessions/${id}`),
  messages: (id: string) => http.get<ChatMessage[]>(`/api/sessions/${id}/messages`),
}
