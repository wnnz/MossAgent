<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import { providersApi } from '@/api/providers'
import type { Provider } from '@/types/domain'
import Dialog from '@/components/ui/Dialog.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Select from '@/components/ui/Select.vue'
import Switch from '@/components/ui/Switch.vue'

const props = defineProps<{ open: boolean; provider: Provider | null }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

const settings = useSettingsStore()
const toast = useToastStore()

const name = ref('')
const type = ref<'openai_compatible' | 'anthropic'>('openai_compatible')
const baseUrl = ref('')
const apiKey = ref('')
const proxyId = ref<string>('')
const enabled = ref(true)
const submitting = ref(false)

watch(() => [props.open, props.provider], () => {
  if (props.open) {
    name.value = props.provider?.name ?? ''
    type.value = props.provider?.type ?? 'openai_compatible'
    baseUrl.value = props.provider?.baseUrl ?? ''
    apiKey.value = props.provider?.apiKey ?? ''
    proxyId.value = props.provider?.proxyId != null ? String(props.provider.proxyId) : ''
    enabled.value = props.provider?.enabled ?? true
  }
})

const title = computed(() => (props.provider ? '编辑提供商' : '新增提供商'))

async function submit() {
  if (!name.value.trim() || !baseUrl.value.trim()) {
    toast.toast('name 和 baseUrl 不能为空', 'error')
    return
  }
  const data = {
    name: name.value.trim(),
    type: type.value,
    baseUrl: baseUrl.value.trim(),
    apiKey: apiKey.value,
    proxyId: proxyId.value ? Number(proxyId.value) : null,
    enabled: enabled.value,
  }
  submitting.value = true
  try {
    if (props.provider) {
      await providersApi.update(props.provider.id, data)
    } else {
      await providersApi.create(data)
    }
    await settings.reloadProviders()
    toast.toast('提供商已保存', 'success')
    emit('update:open', false)
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog :open="props.open" :title="title" @update:open="emit('update:open', $event)">
    <form class="space-y-4" @submit.prevent="submit">
      <div class="space-y-1.5">
        <Label>名称</Label>
        <Input v-model="name" placeholder="例如 DeepSeek / OpenAI / Ollama" />
      </div>
      <div class="space-y-1.5">
        <Label>协议类型</Label>
        <Select v-model="type">
          <option value="openai_compatible">openai_compatible（OpenAI/DeepSeek/Qwen/Ollama 等）</option>
          <option value="anthropic">anthropic</option>
        </Select>
      </div>
      <div class="space-y-1.5">
        <Label>Base URL</Label>
        <Input v-model="baseUrl" placeholder="例如 https://api.deepseek.com" />
      </div>
      <div class="space-y-1.5">
        <Label>API Key（明文存于本地 SQLite）</Label>
        <Input v-model="apiKey" type="password" placeholder="sk-..." />
      </div>
      <div class="space-y-1.5">
        <Label>网络代理（可选）</Label>
        <Select v-model="proxyId">
          <option value="">不使用代理</option>
          <option v-for="p in settings.proxies" :key="p.id" :value="String(p.id)">{{ p.name }}（{{ p.scheme }}://{{ p.host }}:{{ p.port }}）</option>
        </Select>
      </div>
      <div class="flex items-center gap-2">
        <Switch v-model="enabled" />
        <Label>启用</Label>
      </div>
      <Button type="submit" :disabled="submitting">{{ submitting ? '保存中…' : '保存' }}</Button>
    </form>
  </Dialog>
</template>
