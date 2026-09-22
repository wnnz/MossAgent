<script setup lang="ts">
import { ref } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import { providersApi } from '@/api/providers'
import type { Provider, ProviderModel } from '@/types/domain'
import Button from '@/components/ui/Button.vue'
import Badge from '@/components/ui/Badge.vue'
import ProviderDialog from '@/components/settings/ProviderDialog.vue'
import ModelDialog from '@/components/settings/ModelDialog.vue'

const settings = useSettingsStore()
const toast = useToastStore()

const providerDialogOpen = ref(false)
const editingProvider = ref<Provider | null>(null)
const modelDialogOpen = ref(false)
const editingModel = ref<ProviderModel | null>(null)
const refreshing = ref<number | null>(null)

function openCreate() {
  editingProvider.value = null
  providerDialogOpen.value = true
}

function openEdit(provider: Provider) {
  editingProvider.value = provider
  providerDialogOpen.value = true
}

async function remove(provider: Provider) {
  if (!confirm(`确认删除提供商「${provider.name}」？`)) return
  try {
    await providersApi.remove(provider.id)
    await settings.reloadProviders()
    toast.toast('提供商已删除', 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}

async function refresh(provider: Provider) {
  refreshing.value = provider.id
  try {
    const result = await providersApi.refreshModels(provider.id)
    await settings.reloadProviders()
    toast.toast(`模型刷新完成：新增 ${result.added} 个`, 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '刷新失败', 'error')
  } finally {
    refreshing.value = null
  }
}

function openAddModel(provider: Provider) {
  editingProvider.value = provider
  editingModel.value = null
  modelDialogOpen.value = true
}

function openEditModel(provider: Provider, model: ProviderModel) {
  editingProvider.value = provider
  editingModel.value = model
  modelDialogOpen.value = true
}

async function removeModel(provider: Provider, model: ProviderModel) {
  if (!confirm(`确认删除模型「${model.displayName}」？`)) return
  try {
    await providersApi.removeModel(provider.id, model.id)
    await settings.reloadProviders()
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">AI 提供商与模型管理。</p>
      <Button size="sm" @click="openCreate">＋ 新增提供商</Button>
    </div>

    <div v-if="settings.providers.length === 0" class="rounded-md border p-6 text-center text-sm text-muted-foreground">
      暂无提供商，点击右上角新增。
    </div>

    <div v-for="p in settings.providers" :key="p.id" class="rounded-md border p-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="font-medium">{{ p.name }}</span>
          <Badge variant="outline">{{ p.type }}</Badge>
          <Badge v-if="!p.enabled" variant="destructive">已禁用</Badge>
        </div>
        <div class="flex gap-1">
          <Button size="sm" variant="ghost" :disabled="refreshing === p.id" @click="refresh(p)">
            {{ refreshing === p.id ? '刷新中…' : '刷新模型' }}
          </Button>
          <Button size="sm" variant="ghost" @click="openAddModel(p)">＋ 模型</Button>
          <Button size="sm" variant="ghost" @click="openEdit(p)">编辑</Button>
          <Button size="sm" variant="ghost" @click="remove(p)">删除</Button>
        </div>
      </div>
      <p class="mt-1 text-xs text-muted-foreground">{{ p.baseUrl }}</p>
      <div class="mt-2 flex flex-wrap gap-1">
        <button
          v-for="m in p.models"
          :key="m.id"
          class="group inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs hover:bg-accent"
          @click="openEditModel(p, m)"
        >
          {{ m.displayName }}
          <span class="text-muted-foreground group-hover:inline" @click.stop="removeModel(p, m)">✕</span>
        </button>
        <span v-if="p.models.length === 0" class="text-xs text-muted-foreground">无模型（点「刷新模型」拉取）</span>
      </div>
    </div>

    <ProviderDialog v-model:open="providerDialogOpen" :provider="editingProvider" />
    <ModelDialog v-model:open="modelDialogOpen" :provider="editingProvider" :model="editingModel" />
  </div>
</template>
