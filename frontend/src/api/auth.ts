import { http } from '@/lib/http'
import type { AuthStatus } from '@/types/domain'

export const authApi = {
  status: () => http.get<AuthStatus>('/api/auth/status'),
  setup: (password: string) => http.post<AuthStatus>('/api/auth/setup', { password }),
  login: (password: string, rememberMe: boolean) =>
    http.post<AuthStatus>('/api/auth/login', { password, rememberMe }),
  logout: () => http.post<{ message: string }>('/api/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    http.put<{ message: string }>('/api/auth/password', { currentPassword, newPassword }),
}
