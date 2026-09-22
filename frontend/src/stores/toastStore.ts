import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface ToastItem {
  id: number
  message: string
  type: 'success' | 'error' | 'info'
}

let nextId = 1

export const useToastStore = defineStore('toast', () => {
  const toasts = ref<ToastItem[]>([])

  function toast(message: string, type: ToastItem['type'] = 'info') {
    const id = nextId++
    toasts.value.push({ id, message, type })
    setTimeout(() => dismiss(id), 4000)
  }

  function dismiss(id: number) {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  return { toasts, toast, dismiss }
})
