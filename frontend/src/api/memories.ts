import { http } from '@/lib/http'
import type { MemoryItem } from '@/types/domain'

export const memoriesApi = {
  getAll: (scope?: string) => http.get<MemoryItem[]>(`/api/memories${scope ? `?scope=${scope}` : ''}`),
  search: (q: string) => http.get<MemoryItem[]>(`/api/memories/search?q=${encodeURIComponent(q)}`),
  create: (data: { scope: string; title: string; content: string; tags?: string | null }) =>
    http.post<MemoryItem>('/api/memories', data),
  update: (id: number, data: { scope: string; title: string; content: string; tags?: string | null }) =>
    http.put<MemoryItem>(`/api/memories/${id}`, data),
  remove: (id: number) => http.delete<void>(`/api/memories/${id}`),
}
