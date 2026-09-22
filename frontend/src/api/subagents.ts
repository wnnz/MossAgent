import { http } from '@/lib/http'
import type { SubAgent } from '@/types/domain'

export const subAgentsApi = {
  getAll: (enabledOnly = false) => http.get<SubAgent[]>(`/api/subagents?enabledOnly=${enabledOnly}`),
  create: (data: Omit<SubAgent, 'id'>) => http.post<SubAgent>('/api/subagents', data),
  update: (id: number, data: Omit<SubAgent, 'id'>) => http.put<SubAgent>(`/api/subagents/${id}`, data),
  remove: (id: number) => http.delete<void>(`/api/subagents/${id}`),
}
