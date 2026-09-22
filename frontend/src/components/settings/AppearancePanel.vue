<script setup lang="ts">
import { useThemeStore } from '@/stores/themeStore'
import { useToastStore } from '@/stores/toastStore'

const theme = useThemeStore()
const toast = useToastStore()

const options = [
  { value: 'light', label: '浅色' },
  { value: 'dark', label: '深色' },
  { value: 'system', label: '跟随系统' },
]

function setMode(mode: string) {
  theme.setMode(mode as any)
  toast.toast(`主题已切换：${mode}`, 'success')
}
</script>

<template>
  <div class="space-y-4">
    <p class="text-sm text-muted-foreground">三态主题：浅色 / 深色 / 跟随系统（localStorage 持久化）。</p>
    <div class="flex gap-2">
      <button
        v-for="o in options"
        :key="o.value"
        :class="[
          'rounded-md border px-4 py-2 text-sm',
          theme.mode === o.value ? 'border-primary bg-primary/10 font-medium' : 'hover:bg-accent',
        ]"
        @click="setMode(o.value)"
      >
        {{ o.label }}
      </button>
    </div>
  </div>
</template>
