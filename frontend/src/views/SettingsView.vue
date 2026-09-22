<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Tabs from '@/components/ui/Tabs.vue'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import Button from '@/components/ui/Button.vue'
import AppearancePanel from '@/components/settings/AppearancePanel.vue'
import SecurityPanel from '@/components/settings/SecurityPanel.vue'
import WorkspacePanel from '@/components/settings/WorkspacePanel.vue'
import ProvidersPanel from '@/components/settings/ProvidersPanel.vue'
import ProxiesPanel from '@/components/settings/ProxiesPanel.vue'
import SubAgentsPanel from '@/components/settings/SubAgentsPanel.vue'
import MemoriesPanel from '@/components/settings/MemoriesPanel.vue'
import SkillsPanel from '@/components/settings/SkillsPanel.vue'
import McpPanel from '@/components/settings/McpPanel.vue'

const router = useRouter()
const activeTab = ref('appearance')

const tabs = [
  { value: 'appearance', label: '外观' },
  { value: 'security', label: '安全' },
  { value: 'workspace', label: '工作区' },
  { value: 'providers', label: '提供商' },
  { value: 'proxies', label: '代理' },
  { value: 'subagents', label: '子代理' },
  { value: 'memories', label: '记忆' },
  { value: 'skills', label: '技能' },
  { value: 'mcp', label: 'MCP' },
]
</script>

<template>
  <div class="flex h-screen">
    <AppSidebar />
    <main class="flex min-w-0 flex-1 flex-col">
      <header class="flex h-14 items-center justify-between border-b bg-card px-4">
        <h1 class="text-sm font-semibold">设置</h1>
        <Button variant="ghost" size="sm" @click="router.push('/')">返回对话</Button>
      </header>
      <div class="flex-1 overflow-y-auto p-6">
        <div class="mx-auto max-w-3xl space-y-6">
          <Tabs :tabs="tabs" :model-value="activeTab" @update:model-value="activeTab = $event" />
          <AppearancePanel v-if="activeTab === 'appearance'" />
          <SecurityPanel v-else-if="activeTab === 'security'" />
          <WorkspacePanel v-else-if="activeTab === 'workspace'" />
          <ProvidersPanel v-else-if="activeTab === 'providers'" />
          <ProxiesPanel v-else-if="activeTab === 'proxies'" />
          <SubAgentsPanel v-else-if="activeTab === 'subagents'" />
          <MemoriesPanel v-else-if="activeTab === 'memories'" />
          <SkillsPanel v-else-if="activeTab === 'skills'" />
          <McpPanel v-else-if="activeTab === 'mcp'" />
        </div>
      </div>
    </main>
  </div>
</template>
