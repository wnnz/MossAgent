<script setup lang="ts">
import { useToastStore } from '@/stores/toastStore'

const store = useToastStore()
</script>

<template>
  <div class="pointer-events-none fixed bottom-4 right-4 z-[60] flex w-80 flex-col gap-2">
    <div
      v-for="t in store.toasts"
      :key="t.id"
      :class="[
        'pointer-events-auto rounded-md border px-4 py-3 text-sm shadow-lg',
        t.type === 'success' ? 'border-emerald-500/40 bg-emerald-500/10 text-foreground' : '',
        t.type === 'error' ? 'border-destructive/40 bg-destructive/10 text-foreground' : '',
        t.type === 'info' ? 'bg-popover text-popover-foreground' : '',
      ]"
      @click="store.dismiss(t.id)"
    >
      {{ t.message }}
    </div>
  </div>
</template>
