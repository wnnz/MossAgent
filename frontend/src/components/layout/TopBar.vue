<script setup lang="ts">
import { computed } from 'vue'
import { useSessionStore } from '@/stores/sessionStore'
import { useSettingsStore } from '@/stores/settingsStore'
import ModelPicker from '@/components/chat/ModelPicker.vue'

const sessions = useSessionStore()
const settings = useSettingsStore()

const provider = computed(() => settings.providers.find((p) => p.id === sessions.current?.providerId) ?? null)
</script>

<template>
  <header class="flex h-14 items-center justify-between border-b bg-card px-4">
    <div class="flex min-w-0 items-center gap-3">
      <h1 class="truncate text-sm font-semibold">{{ sessions.current?.title ?? 'Coding Agent' }}</h1>
    </div>
    <div class="flex items-center gap-2">
      <ModelPicker
        v-if="sessions.current"
        :provider-id="sessions.current.providerId"
        :model-id="sessions.current.modelId"
        @change="(pid, mid) => sessions.update(sessions.current!.id, { providerId: pid, modelId: mid })"
      />
      <span v-if="provider" class="text-xs text-muted-foreground">{{ provider.type === 'anthropic' ? 'Anthropic' : 'OpenAI 兼容' }}</span>
    </div>
  </header>
</template>
