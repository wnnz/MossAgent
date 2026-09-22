import { http } from '@/lib/http'
import type { AppSetting } from '@/types/domain'

export const settingsApi = {
  getAll: () => http.get<AppSetting[]>('/api/settings'),
  update: (key: string, value: string) => http.put<{ key: string; value: string }>('/api/settings', { key, value }),
}
