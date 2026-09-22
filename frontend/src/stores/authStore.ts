import { defineStore } from 'pinia'
import { ref } from 'vue'
import { authApi } from '@/api/auth'

export const useAuthStore = defineStore('auth', () => {
  const needsSetup = ref(false)
  const authenticated = ref(false)
  const loaded = ref(false)

  async function refreshStatus() {
    const status = await authApi.status()
    needsSetup.value = status.needsSetup
    authenticated.value = status.authenticated
    loaded.value = true
  }

  async function setup(password: string) {
    await authApi.setup(password)
    await refreshStatus()
  }

  async function login(password: string, rememberMe: boolean) {
    await authApi.login(password, rememberMe)
    await refreshStatus()
  }

  async function logout() {
    await authApi.logout()
    authenticated.value = false
  }

  async function changePassword(currentPassword: string, newPassword: string) {
    await authApi.changePassword(currentPassword, newPassword)
  }

  return { needsSetup, authenticated, loaded, refreshStatus, setup, login, logout, changePassword }
})
