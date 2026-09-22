<script setup lang="ts">
import { computed } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'

const props = withDefaults(defineProps<{
  providerId: number
  modelId: string
  compact?: boolean
}>(), { compact: false })
const emit = defineEmits<{ (e: 'change', providerId: number, modelId: string): void }>()

const settings = useSettingsStore()
const provider = computed(() => settings.providers.find((p) => p.id === props.providerId))

function onProviderChange(providerId: number) {
  const next = settings.providers.find((p) => p.id === providerId)
  const modelId = next?.models[0]?.modelId ?? ''
  if (modelId) emit('change', providerId, modelId)
}
</script>

<template>
  <div class="flex items-center gap-1.5">
    <!-- 提供商选择 -->
    <div class="relative inline-flex items-center">
      <select
        :value="props.providerId"
        :class="[
          'appearance-none rounded-lg border border-border/70 bg-muted/40 font-medium text-muted-foreground transition-colors hover:border-border hover:bg-muted/70 hover:text-foreground focus:outline-none focus:ring-1 focus:ring-ring cursor-pointer',
          props.compact ? 'h-7 pl-2.5 pr-6 text-xs max-w-[110px] truncate' : 'h-8 pl-3 pr-7 text-xs max-w-[140px]',
        ]"
        @change="onProviderChange(Number(($event.target as HTMLSelectElement).value))"
      >
        <option v-for="p in settings.providers.filter((p) => p.enabled)" :key="p.id" :value="p.id" class="bg-popover text-popover-foreground">
          {{ p.name }}
        </option>
      </select>
      <svg class="pointer-events-none absolute right-1.5 h-3 w-3 text-muted-foreground/70" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
      </svg>
    </div>

    <!-- 模型选择 -->
    <div v-if="provider" class="relative inline-flex items-center">
      <select
        :value="props.modelId"
        :class="[
          'appearance-none rounded-lg border border-border/70 bg-muted/40 font-medium text-foreground transition-colors hover:border-border hover:bg-muted/70 focus:outline-none focus:ring-1 focus:ring-ring cursor-pointer',
          props.compact ? 'h-7 pl-2.5 pr-6 text-xs max-w-[160px] truncate' : 'h-8 pl-3 pr-7 text-xs max-w-[200px]',
        ]"
        @change="emit('change', props.providerId, ($event.target as HTMLSelectElement).value)"
      >
        <option v-for="m in provider.models" :key="m.modelId" :value="m.modelId" class="bg-popover text-popover-foreground">
          {{ m.displayName }}
        </option>
      </select>
      <svg class="pointer-events-none absolute right-1.5 h-3 w-3 text-muted-foreground/70" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
      </svg>
    </div>
  </div>
</template>
