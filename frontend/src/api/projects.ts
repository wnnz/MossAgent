import { http } from '@/lib/http'

export interface Project {
  id: number
  name: string
  path: string
  createdAt: string
}

export const projectsApi = {
  getAll: () => http.get<Project[]>('/api/projects'),
  create: (data: { name: string; path?: string }) => http.post<Project>('/api/projects', data),
  remove: (id: number) => http.delete<void>(`/api/projects/${id}`),
  branch: (id: number) => http.get<{ branch: string }>(`/api/projects/${id}/branch`),
}
