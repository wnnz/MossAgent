<script setup lang="ts">
import { onMounted } from 'vue'
import { useSessionStore } from '@/stores/sessionStore'
import { useSettingsStore } from '@/stores/settingsStore'
import { useProjectStore } from '@/stores/projectStore'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import MessageList from '@/components/chat/MessageList.vue'
import Composer from '@/components/chat/Composer.vue'

const sessions = useSessionStore()
const settings = useSettingsStore()
const projectStore = useProjectStore()

onMounted(async () => {
  await settings.loadAll().catch(() => {})
  await projectStore.loadAll().catch(() => {})
  await sessions.loadSessions().catch(() => {})
})
</script>

<template>
  <div class="flex h-screen overflow-hidden bg-background text-foreground">
    <AppSidebar />
    <main class="relative flex min-w-0 flex-1 flex-col overflow-hidden bg-background">
      <MessageList />
      <Composer />
    </main>
  </div>
</template>
