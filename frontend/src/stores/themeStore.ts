import { defineStore } from 'pinia'
import { ref } from 'vue'
import { applyTheme, loadTheme, saveTheme, watchSystemTheme, type ThemeMode } from '@/lib/theme'

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>(loadTheme())

  function apply() {
    applyTheme(mode.value)
    watchSystemTheme(() => {
      if (mode.value === 'system') applyTheme('system')
    })
  }

  function setMode(next: ThemeMode) {
    mode.value = next
    saveTheme(next)
    applyTheme(next)
  }

  return { mode, apply, setMode }
})
