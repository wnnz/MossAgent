<script setup lang="ts">
import { watch, onUnmounted } from 'vue'

const props = defineProps<{ open: boolean; title?: string; wide?: boolean }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

function close() {
  emit('update:open', false)
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') close()
}

watch(() => props.open, (open) => {
  if (open) document.addEventListener('keydown', onKeydown)
  else document.removeEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4">
      <!-- 遮罩（带微高斯模糊） -->
      <div class="fixed inset-0 bg-black/40 backdrop-blur-xs transition-opacity" @click="close" />
      <!-- 弹窗容器 -->
      <div
        :class="[
          'relative z-10 max-h-[85vh] w-full overflow-y-auto rounded-2xl border border-border/80 bg-card p-6 shadow-2xl transition-all',
          props.wide ? 'max-w-2xl' : 'max-w-lg',
        ]"
      >
        <div class="mb-4 flex items-center justify-between">
          <h2 class="text-base font-semibold tracking-tight text-foreground">{{ title }}</h2>
          <button
            class="flex h-7 w-7 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
            @click="close"
          >
            <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <slot />
      </div>
    </div>
  </Teleport>
</template>
