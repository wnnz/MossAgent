// 主题应用：light / dark / system
export type ThemeMode = 'light' | 'dark' | 'system'

const THEME_KEY = 'codingagent-theme'

export function loadTheme(): ThemeMode {
  return (localStorage.getItem(THEME_KEY) as ThemeMode) || 'system'
}

export function saveTheme(mode: ThemeMode): void {
  localStorage.setItem(THEME_KEY, mode)
}

export function applyTheme(mode: ThemeMode): void {
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)')
  const effective = mode === 'system' ? (prefersDark.matches ? 'dark' : 'light') : mode
  document.documentElement.classList.toggle('dark', effective === 'dark')
}

export function watchSystemTheme(onChange: () => void): void {
  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', onChange)
}
