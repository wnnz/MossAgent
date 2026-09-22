<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useToastStore } from '@/stores/toastStore'
import { mcpApi } from '@/api/mcp'
import type { McpServer } from '@/types/domain'
import Dialog from '@/components/ui/Dialog.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Select from '@/components/ui/Select.vue'
import Switch from '@/components/ui/Switch.vue'

const props = defineProps<{ open: boolean; server: McpServer | null }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

const toast = useToastStore()

const name = ref('')
const transport = ref<'stdio' | 'http'>('stdio')
const command = ref('')
const argsJson = ref('[]')
const envJson = ref('{}')
const url = ref('')
const headersJson = ref('{}')
const enabled = ref(true)
const autoConnect = ref(false)
const submitting = ref(false)

watch(() => [props.open, props.server], () => {
  if (props.open) {
    name.value = props.server?.name ?? ''
    transport.value = props.server?.transport ?? 'stdio'
    command.value = props.server?.command ?? ''
    argsJson.value = props.server?.argsJson ?? '[]'
    envJson.value = props.server?.envJson ?? '{}'
    url.value = props.server?.url ?? ''
    headersJson.value = props.server?.headersJson ?? '{}'
    enabled.value = props.server?.enabled ?? true
    autoConnect.value = props.server?.autoConnect ?? false
  }
})

const title = computed(() => (props.server ? '编辑 MCP 服务器' : '新增 MCP 服务器'))

function validateJson(raw: string, fallback: string, label: string): boolean {
  try {
    JSON.parse(raw || fallback)
    return true
  } catch {
    toast.toast(`${label} 必须是合法 JSON`, 'error')
    return false
  }
}

async function submit() {
  if (!name.value.trim()) {
    toast.toast('name 不能为空', 'error')
    return
  }
  if (transport.value === 'stdio' && !command.value.trim()) {
    toast.toast('stdio 传输必须提供 command', 'error')
    return
  }
  if (transport.value === 'http' && !url.value.trim()) {
    toast.toast('http 传输必须提供 url', 'error')
    return
  }
  if (!validateJson(argsJson.value, '[]', '参数 JSON') || !validateJson(envJson.value, '{}', '环境变量 JSON') || !validateJson(headersJson.value, '{}', '请求头 JSON')) {
    return
  }
  const data = {
    name: name.value.trim(),
    transport: transport.value,
    command: command.value.trim() || null,
    argsJson: argsJson.value || '[]',
    envJson: envJson.value || '{}',
    url: url.value.trim() || null,
    headersJson: headersJson.value || '{}',
    enabled: enabled.value,
    autoConnect: autoConnect.value,
  }
  submitting.value = true
  try {
    if (props.server) {
      await mcpApi.updateServer(props.server.id, data)
    } else {
      await mcpApi.createServer(data)
    }
    toast.toast('MCP 服务器已保存', 'success')
    emit('update:open', false)
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog :open="props.open" :title="title" wide @update:open="emit('update:open', $event)">
    <form class="space-y-4" @submit.prevent="submit">
      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1.5">
          <Label>名称</Label>
          <Input v-model="name" placeholder="例如 filesystem" />
        </div>
        <div class="space-y-1.5">
          <Label>传输</Label>
          <Select v-model="transport">
            <option value="stdio">stdio（本地进程 JSON-RPC）</option>
            <option value="http">http（streamable HTTP）</option>
          </Select>
        </div>
      </div>

      <template v-if="transport === 'stdio'">
        <div class="space-y-1.5">
          <Label>命令</Label>
          <Input v-model="command" placeholder="例如 node / npx" />
        </div>
        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <Label>参数（JSON 数组）</Label>
            <Input v-model="argsJson" placeholder='["-y","@modelcontextprotocol/server-filesystem","/path"]' />
          </div>
          <div class="space-y-1.5">
            <Label>环境变量（JSON 对象）</Label>
            <Input v-model="envJson" placeholder="{}" />
          </div>
        </div>
      </template>

      <template v-else>
        <div class="space-y-1.5">
          <Label>URL</Label>
          <Input v-model="url" placeholder="https://example.com/mcp" />
        </div>
        <div class="space-y-1.5">
          <Label>请求头（JSON 对象）</Label>
          <Input v-model="headersJson" placeholder='{"Authorization": "Bearer ..."}' />
        </div>
      </template>

      <div class="flex flex-col gap-2">
        <div class="flex items-center gap-2"><Switch v-model="enabled" /><Label>启用</Label></div>
        <div class="flex items-center gap-2"><Switch v-model="autoConnect" /><Label>启动时自动连接</Label></div>
      </div>
      <Button type="submit" :disabled="submitting">{{ submitting ? '保存中…' : '保存' }}</Button>
    </form>
  </Dialog>
</template>
