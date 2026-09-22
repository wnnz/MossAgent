<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useToastStore } from '@/stores/toastStore'
import { subAgentsApi } from '@/api/subagents'
import type { SubAgent } from '@/types/domain'
import Button from '@/components/ui/Button.vue'
import Badge from '@/components/ui/Badge.vue'
import SubAgentDialog from '@/components/settings/SubAgentDialog.vue'

const toast = useToastStore()
const subAgents = ref<SubAgent[]>([])
const dialogOpen = ref(false)
const editing = ref<SubAgent | null>(null)

async function reload() {
  subAgents.value = await subAgentsApi.getAll()
}

onMounted(reload)

function openCreate() {
  editing.value = null
  dialogOpen.value = true
}

function openEdit(subAgent: SubAgent) {
  editing.value = subAgent
  dialogOpen.value = true
}

async function remove(subAgent: SubAgent) {
  if (!confirm(`确认删除子代理「${subAgent.name}」？`)) return
  try {
    await subAgentsApi.remove(subAgent.id)
    await reload()
    toast.toast('子代理已删除', 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">子代理：独立 Agent 循环，主代理按调用规则通过 task 工具触发。</p>
      <Button size="sm" @click="openCreate">＋ 新增子代理</Button>
    </div>

    <div v-if="subAgents.length === 0" class="rounded-md border p-6 text-center text-sm text-muted-foreground">
      暂无子代理。
    </div>

    <div v-for="s in subAgents" :key="s.id" class="rounded-md border p-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="font-medium">{{ s.name }}</span>
          <Badge variant="outline">{{ s.modelId }}</Badge>
          <Badge v-if="!s.enabled" variant="destructive">已禁用</Badge>
        </div>
        <div class="flex gap-1">
          <Button size="sm" variant="ghost" @click="openEdit(s)">编辑</Button>
          <Button size="sm" variant="ghost" @click="remove(s)">删除</Button>
        </div>
      </div>
      <p class="mt-1 text-xs text-muted-foreground">{{ s.description }}</p>
      <p v-if="s.invocationRule" class="mt-1 text-xs text-muted-foreground">何时调用：{{ s.invocationRule }}</p>
    </div>

    <SubAgentDialog v-model:open="dialogOpen" :sub-agent="editing" />
  </div>
</template>
