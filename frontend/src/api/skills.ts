import { http } from '@/lib/http'
import type { Skill } from '@/types/domain'

export const skillsApi = {
  getAll: (enabledOnly = false) => http.get<Skill[]>(`/api/skills?enabledOnly=${enabledOnly}`),
  create: (data: { name: string; description: string; instructions: string; enabled: boolean }) =>
    http.post<Skill>('/api/skills', data),
  update: (id: number, data: { name: string; description: string; instructions: string; enabled: boolean }) =>
    http.put<Skill>(`/api/skills/${id}`, data),
  toggle: (id: number) => http.patch<Skill>(`/api/skills/${id}/toggle`, {}),
  remove: (id: number) => http.delete<void>(`/api/skills/${id}`),
}
