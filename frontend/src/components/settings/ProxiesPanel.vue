<script setup lang="ts">
import { ref } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import { proxiesApi } from '@/api/proxies'
import type { Proxy } from '@/types/domain'
import Button from '@/components/ui/Button.vue'
import Badge from '@/components/ui/Badge.vue'
import ProxyDialog from '@/components/settings/ProxyDialog.vue'

const settings = useSettingsStore()
const toast = useToastStore()

const dialogOpen = ref(false)
const editing = ref<Proxy | null>(null)
const testing = ref<number | null>(null)

function openCreate() {
  editing.value = null
  dialogOpen.value = true
}

function openEdit(proxy: Proxy) {
  editing.value = proxy
  dialogOpen.value = true
}

async function remove(proxy: Proxy) {
  if (!confirm(`确认删除代理「${proxy.name}」？`)) return
  try {
    await proxiesApi.remove(proxy.id)
    await settings.reloadProxies()
    toast.toast('代理已删除', 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}

async function test(proxy: Proxy) {
  testing.value = proxy.id
  try {
    const result = await proxiesApi.test(proxy.id)
    if (result.success) {
      toast.toast(`${proxy.name}: ${result.message}（${result.latencyMs}ms）`, 'success')
    } else {
      toast.toast(`${proxy.name}: ${result.message}`, 'error')
    }
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '测试失败', 'error')
  } finally {
    testing.value = null
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">网络代理（HTTP / SOCKS5），供 AI 提供商请求走代理。</p>
      <Button size="sm" @click="openCreate">＋ 新增代理</Button>
    </div>

    <div v-if="settings.proxies.length === 0" class="rounded-md border p-6 text-center text-sm text-muted-foreground">
      暂无代理。
    </div>

    <div v-for="p in settings.proxies" :key="p.id" class="flex items-center justify-between rounded-md border p-3">
      <div class="flex items-center gap-2">
        <span class="font-medium">{{ p.name }}</span>
        <Badge variant="outline">{{ p.scheme }}</Badge>
        <span class="text-xs text-muted-foreground">{{ p.host }}:{{ p.port }}</span>
        <Badge v-if="!p.enabled" variant="destructive">已禁用</Badge>
      </div>
      <div class="flex gap-1">
        <Button size="sm" variant="ghost" :disabled="testing === p.id" @click="test(p)">
          {{ testing === p.id ? '测试中…' : '测试' }}
        </Button>
        <Button size="sm" variant="ghost" @click="openEdit(p)">编辑</Button>
        <Button size="sm" variant="ghost" @click="remove(p)">删除</Button>
      </div>
    </div>

    <ProxyDialog v-model:open="dialogOpen" :proxy="editing" />
  </div>
</template>
