<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import { proxiesApi } from '@/api/proxies'
import type { Proxy } from '@/types/domain'
import Dialog from '@/components/ui/Dialog.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Select from '@/components/ui/Select.vue'
import Switch from '@/components/ui/Switch.vue'

const props = defineProps<{ open: boolean; proxy: Proxy | null }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

const settings = useSettingsStore()
const toast = useToastStore()

const name = ref('')
const scheme = ref<'http' | 'socks5'>('http')
const host = ref('')
const port = ref(1080)
const username = ref('')
const password = ref('')
const enabled = ref(true)
const submitting = ref(false)

watch(() => [props.open, props.proxy], () => {
  if (props.open) {
    name.value = props.proxy?.name ?? ''
    scheme.value = props.proxy?.scheme ?? 'http'
    host.value = props.proxy?.host ?? ''
    port.value = props.proxy?.port ?? 1080
    username.value = props.proxy?.username ?? ''
    password.value = props.proxy?.password ?? ''
    enabled.value = props.proxy?.enabled ?? true
  }
})

const title = computed(() => (props.proxy ? '编辑代理' : '新增代理'))

async function submit() {
  if (!name.value.trim() || !host.value.trim() || port.value <= 0) {
    toast.toast('name、host 必填且 port > 0', 'error')
    return
  }
  const data = {
    name: name.value.trim(),
    scheme: scheme.value,
    host: host.value.trim(),
    port: port.value,
    username: username.value || null,
    password: password.value || null,
    enabled: enabled.value,
  }
  submitting.value = true
  try {
    if (props.proxy) {
      await proxiesApi.update(props.proxy.id, data)
    } else {
      await proxiesApi.create(data)
    }
    await settings.reloadProxies()
    toast.toast('代理已保存', 'success')
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
        <Input v-model="name" />
      </div>
      <div class="space-y-1.5">
        <Label>协议</Label>
        <Select v-model="scheme">
          <option value="http">http</option>
          <option value="socks5">socks5</option>
        </Select>
      </div>
      <div class="grid grid-cols-[1fr_120px] gap-4">
        <div class="space-y-1.5">
          <Label>主机</Label>
          <Input v-model="host" placeholder="127.0.0.1" />
        </div>
        <div class="space-y-1.5">
          <Label>端口</Label>
          <Input v-model="port" type="number" />
        </div>
      </div>
      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1.5">
          <Label>用户名（可选）</Label>
          <Input v-model="username" />
        </div>
        <div class="space-y-1.5">
          <Label>密码（可选）</Label>
          <Input v-model="password" type="password" />
        </div>
      </div>
      <div class="flex items-center gap-2">
        <Switch v-model="enabled" />
        <Label>启用</Label>
      </div>
      <Button type="submit" :disabled="submitting">{{ submitting ? '保存中…' : '保存' }}</Button>
    </form>
  </Dialog>
</template>
