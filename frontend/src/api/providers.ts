import { http } from '@/lib/http'
import type { Provider, ProviderModel, ReasoningEffort } from '@/types/domain'

export interface ModelRefreshResult {
  added: number
  updated: number
  models: ProviderModel[]
}

export const providersApi = {
  getAll: () => http.get<Provider[]>('/api/providers'),
  get: (id: number) => http.get<Provider>(`/api/providers/${id}`),
  create: (data: { name: string; type: string; baseUrl: string; apiKey: string; proxyId?: number | null; enabled: boolean }) =>
    http.post<Provider>('/api/providers', data),
  update: (id: number, data: { name: string; type: string; baseUrl: string; apiKey: string; proxyId?: number | null; enabled: boolean }) =>
    http.put<Provider>(`/api/providers/${id}`, data),
  remove: (id: number) => http.delete<void>(`/api/providers/${id}`),
  refreshModels: (id: number) => http.post<ModelRefreshResult>(`/api/providers/${id}/models/refresh`),
  addModel: (id: number, data: { modelId: string; displayName: string; supportsTools: boolean; supportsReasoning: boolean; defaultReasoningEffort: ReasoningEffort; maxContextTokens: number; maxOutputTokens: number }) =>
    http.post<ProviderModel>(`/api/providers/${id}/models`, data),
  updateModel: (id: number, modelId: number, data: { modelId: string; displayName: string; supportsTools: boolean; supportsReasoning: boolean; defaultReasoningEffort: ReasoningEffort; maxContextTokens: number; maxOutputTokens: number; isCustom: boolean }) =>
    http.put<ProviderModel>(`/api/providers/${id}/models/${modelId}`, data),
  removeModel: (id: number, modelId: number) => http.delete<void>(`/api/providers/${id}/models/${modelId}`),
}
