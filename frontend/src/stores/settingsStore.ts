import { defineStore } from 'pinia'
import { ref } from 'vue'
import { settingsApi } from '@/api/settings'
import { providersApi } from '@/api/providers'
import { proxiesApi } from '@/api/proxies'
import type { AppSetting, Provider, Proxy } from '@/types/domain'

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<AppSetting[]>([])
  const providers = ref<Provider[]>([])
  const proxies = ref<Proxy[]>([])
  const loading = ref(false)

  async function loadAll() {
    loading.value = true
    try {
      const [s, p, x] = await Promise.all([settingsApi.getAll(), providersApi.getAll(), proxiesApi.getAll()])
      settings.value = s
      providers.value = p
      proxies.value = x
    } finally {
      loading.value = false
    }
  }

  async function updateSetting(key: string, value: string) {
    await settingsApi.update(key, value)
    const item = settings.value.find((s) => s.key === key)
    if (item) item.value = value
    else settings.value.push({ key, value })
  }

  async function reloadProviders() {
    providers.value = await providersApi.getAll()
  }

  async function reloadProxies() {
    proxies.value = await proxiesApi.getAll()
  }

  function getSetting(key: string): string {
    return settings.value.find((s) => s.key === key)?.value ?? ''
  }

  return { settings, providers, proxies, loading, loadAll, updateSetting, reloadProviders, reloadProxies, getSetting }
})
