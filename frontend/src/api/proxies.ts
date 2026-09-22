import { http } from '@/lib/http'
import type { Proxy } from '@/types/domain'

export interface ProxyTestResult {
  success: boolean
  message: string
  latencyMs: number | null
}

export const proxiesApi = {
  getAll: () => http.get<Proxy[]>('/api/proxies'),
  create: (data: { name: string; scheme: string; host: string; port: number; username?: string | null; password?: string | null; enabled: boolean }) =>
    http.post<Proxy>('/api/proxies', data),
  update: (id: number, data: { name: string; scheme: string; host: string; port: number; username?: string | null; password?: string | null; enabled: boolean }) =>
    http.put<Proxy>(`/api/proxies/${id}`, data),
  remove: (id: number) => http.delete<void>(`/api/proxies/${id}`),
  test: (id: number) => http.post<ProxyTestResult>(`/api/proxies/${id}/test`),
}
