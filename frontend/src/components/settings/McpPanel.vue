<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useToastStore } from '@/stores/toastStore'
import { mcpApi } from '@/api/mcp'
import type { McpServer, McpTool } from '@/types/domain'
import Button from '@/components/ui/Button.vue'
import Badge from '@/components/ui/Badge.vue'
import Dialog from '@/components/ui/Dialog.vue'
import McpServerDialog from '@/components/settings/McpServerDialog.vue'

const toast = useToastStore()
const servers = ref<McpServer[]>([])
const toolsByServer = ref<Record<number, McpTool[]>>({})
const dialogOpen = ref(false)
const editing = ref<McpServer | null>(null)
const connecting = ref<number | null>(null)
const calling = ref<number | null>(null)
const toolDialogOpen = ref(false)
const callingTool = ref<{ server: McpServer; tool: McpTool } | null>(null)
const toolArguments = ref('{}')
const toolResult = ref<string | null>(null)

async function reload() {
  servers.value = await mcpApi.getServers()
  for (const server of servers.value) {
    try {
      toolsByServer.value[server.id] = await mcpApi.tools(server.id)
    } catch {
      toolsByServer.value[server.id] = []
    }
  }
}

onMounted(reload)

function openCreate() {
  editing.value = null
  dialogOpen.value = true
}

function openEdit(server: McpServer) {
  editing.value = server
  dialogOpen.value = true
}

async function remove(server: McpServer) {
  if (!confirm(`确认删除 MCP 服务器「${server.name}」？`)) return
  try {
    await mcpApi.removeServer(server.id)
    await reload()
    toast.toast('已删除', 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}

async function connect(server: McpServer) {
  connecting.value = server.id
  try {
    const tools = await mcpApi.connect(server.id)
    await reload()
    toast.toast(`已连接「${server.name}」，发现 ${tools.length} 个工具`, 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '连接失败', 'error')
  } finally {
    connecting.value = null
  }
}

async function disconnect(server: McpServer) {
  try {
    await mcpApi.disconnect(server.id)
    toast.toast(`已断开「${server.name}」`, 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '断开失败', 'error')
  }
}

function openCallTool(server: McpServer, tool: McpTool) {
  callingTool.value = { server, tool }
  toolArguments.value = '{}'
  toolResult.value = null
  toolDialogOpen.value = true
}

async function callTool() {
  if (!callingTool.value) return
  const { server, tool } = callingTool.value
  calling.value = server.id
  try {
    let args: Record<string, unknown> = {}
    try {
      args = JSON.parse(toolArguments.value || '{}')
    } catch {
      toast.toast('参数必须是合法 JSON', 'error')
      return
    }
    const result = await mcpApi.callTool(server.id, tool.name, args)
    toolResult.value = result.result
    if (!result.success) toast.toast('工具执行返回错误', 'error')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '调用失败', 'error')
  } finally {
    calling.value = null
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">MCP 服务器（stdio / streamable HTTP），连接后工具桥接为 mcp__&lt;server&gt;__&lt;tool&gt;。</p>
      <Button size="sm" @click="openCreate">＋ 新增服务器</Button>
    </div>

    <div v-if="servers.length === 0" class="rounded-md border p-6 text-center text-sm text-muted-foreground">暂无 MCP 服务器。</div>

    <div v-for="s in servers" :key="s.id" class="rounded-md border p-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="font-medium">{{ s.name }}</span>
          <Badge variant="outline">{{ s.transport }}</Badge>
          <Badge v-if="!s.enabled" variant="destructive">已禁用</Badge>
        </div>
        <div class="flex gap-1">
          <Button size="sm" variant="ghost" :disabled="connecting === s.id" @click="connect(s)">
            {{ connecting === s.id ? '连接中…' : '连接' }}
          </Button>
          <Button size="sm" variant="ghost" @click="disconnect(s)">断开</Button>
          <Button size="sm" variant="ghost" @click="openEdit(s)">编辑</Button>
          <Button size="sm" variant="ghost" @click="remove(s)">删除</Button>
        </div>
      </div>
      <p class="mt-1 truncate text-xs text-muted-foreground">{{ s.transport === 'stdio' ? `${s.command} ${s.argsJson ?? ''}` : s.url }}</p>
      <div class="mt-2 flex flex-wrap gap-1">
        <button
          v-for="t in toolsByServer[s.id] ?? []"
          :key="t.id"
          class="rounded-md border px-2 py-0.5 font-mono text-xs hover:bg-accent"
          @click="openCallTool(s, t)"
        >
          {{ t.name }}
        </button>
        <span v-if="(toolsByServer[s.id] ?? []).length === 0" class="text-xs text-muted-foreground">无工具缓存（连接后获取）</span>
      </div>
    </div>

    <McpServerDialog v-model:open="dialogOpen" :server="editing" />

    <Dialog :open="toolDialogOpen" :title="callingTool ? `测试调用 ${callingTool.tool.name}` : '测试调用'" @update:open="toolDialogOpen = $event">
      <div class="space-y-3">
        <p class="text-xs text-muted-foreground">参数（JSON 对象）</p>
        <textarea
          v-model="toolArguments"
          rows="5"
          class="w-full rounded-md border border-input bg-transparent px-3 py-2 font-mono text-xs"
        />
        <Button :disabled="calling !== null" @click="callTool">{{ calling !== null ? '调用中…' : '调用' }}</Button>
        <div v-if="toolResult !== null">
          <p class="mb-1 text-xs text-muted-foreground">结果</p>
          <pre class="max-h-64 overflow-auto rounded bg-muted p-2 text-xs whitespace-pre-wrap">{{ toolResult || '（空）' }}</pre>
        </div>
      </div>
    </Dialog>
  </div>
</template>
