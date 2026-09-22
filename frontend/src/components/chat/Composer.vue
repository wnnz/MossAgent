<script setup lang="ts">
import { computed, ref, watch, nextTick } from 'vue'
import { useSessionStore } from '@/stores/sessionStore'
import { useProjectStore } from '@/stores/projectStore'
import { projectsApi } from '@/api/projects'
import ModelPicker from '@/components/chat/ModelPicker.vue'
import type { ReasoningEffort } from '@/types/domain'
import ReasoningPicker from '@/components/chat/ReasoningPicker.vue'
import { useToastStore } from '@/stores/toastStore'

const sessions = useSessionStore()
const projectStore = useProjectStore()
const toast = useToastStore()

const input = ref('')
const reasoningOverride = ref('')
const textareaRef = ref<HTMLTextAreaElement | null>(null)

function adjustHeight() {
  const el = textareaRef.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = `${Math.min(Math.max(el.scrollHeight, 48), 200)}px`
}

watch(input, () => {
  nextTick(adjustHeight)
})

const project = computed(() => {
  const pid = sessions.current?.projectId
  if (pid != null) {
    const p = projectStore.projects.find((x) => x.id === pid)
    if (p) return p
  }
  return projectStore.current
})

const branch = ref('main')
// 项目变化时刷新分支
watch(() => project.value?.id, watchBranch, { immediate: true })

function watchBranch() {
  const pid = project.value?.id
  if (pid != null) {
    projectsApi.branch(pid).then((r) => (branch.value = r.branch)).catch(() => (branch.value = 'main'))
  }
}

async function send() {
  const message = input.value.trim()
  if (!message || sessions.running) return
  input.value = ''
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
  await sessions.send(message, (reasoningOverride.value || null) as ReasoningEffort | null)
}

async function stop() {
  await sessions.stop()
}

function onKeydown(e: KeyboardEvent) {
  // Enter 发送，Shift+Enter 换行
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault()
    void send()
  }
}
</script>

<template>
  <div class="w-full max-w-3xl mx-auto px-4 pb-4 pt-1">
    <!-- 错误横幅 -->
    <div
      v-if="sessions.error"
      class="mb-2.5 flex items-center justify-between rounded-xl border border-destructive/30 bg-destructive/10 px-3.5 py-2 text-xs text-destructive"
    >
      <div class="flex items-center gap-2">
        <svg class="h-4 w-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
        </svg>
        <span>{{ sessions.error }}</span>
      </div>
      <button class="text-xs font-semibold hover:opacity-75" @click="sessions.error = null">✕</button>
    </div>

    <!-- 浮动式卡片输入框 -->
    <div
      class="relative rounded-2xl border border-border/80 bg-card/85 p-3 shadow-lg shadow-black/[0.02] backdrop-blur-md transition-all duration-200 focus-within:border-primary/40 focus-within:shadow-md dark:shadow-black/25"
    >
      <!-- 顶部上下文标签栏（项目 | 本地沙箱 | 分支） -->
      <div class="mb-2 flex flex-wrap items-center gap-1.5 text-[11px] text-muted-foreground">
        <!-- 项目胶囊 -->
        <div class="flex items-center gap-1.5 rounded-md bg-muted/50 px-2 py-0.5 transition-colors hover:bg-muted">
          <svg class="h-3 w-3 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-8.69-6.44l-2.12-2.12a1.5 1.5 0 00-1.061-.44H4.5A2.25 2.25 0 002.25 6v12a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9a2.25 2.25 0 00-2.25-2.25h-5.379a1.5 1.5 0 01-1.06-.44z" />
          </svg>
          <span class="font-medium text-foreground">{{ project?.name ?? '默认工作区' }}</span>
        </div>

        <span class="text-border/80">•</span>

        <!-- 运行环境 -->
        <div class="flex items-center gap-1.5 rounded-md bg-muted/40 px-2 py-0.5 text-muted-foreground">
          <svg class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 01-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0115 18.257V17.25m6-12V15a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 15V5.25m18 0A2.25 2.25 0 0018.75 3H5.25A2.25 2.25 0 003 5.25m18 0H3" />
          </svg>
          <span>本地沙箱</span>
        </div>

        <span class="text-border/80">•</span>

        <!-- Git 分支 -->
        <div class="flex items-center gap-1 rounded-md bg-muted/40 px-2 py-0.5 text-muted-foreground">
          <svg class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M7.5 3v13.5m0 0a3 3 0 106 0v-1.5m-6 1.5a3 3 0 11-6 0m6 0v-1.5m6-6a3 3 0 100-6 3 3 0 000 6z" />
          </svg>
          <span class="font-mono text-foreground">{{ branch }}</span>
        </div>
      </div>

      <!-- 核心输入框 -->
      <textarea
        ref="textareaRef"
        v-model="input"
        rows="2"
        placeholder="随心描述任务或提问，Enter 发送，Shift+Enter 换行…"
        class="w-full resize-none bg-transparent px-1 py-1 text-sm leading-relaxed text-foreground placeholder:text-muted-foreground/60 focus:outline-none"
        @keydown="onKeydown"
      />

      <!-- 底部控制栏 -->
      <div class="mt-2.5 flex flex-wrap items-center justify-between gap-2 pt-1">
        <!-- 左侧功能按键 -->
        <div class="flex items-center gap-1 text-muted-foreground">
          <button
            class="flex h-7 w-7 items-center justify-center rounded-lg transition-colors hover:bg-muted hover:text-foreground"
            title="附加文件（开发中）"
            @click="toast.toast('附件功能开发中', 'info')"
          >
            <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
            </svg>
          </button>

          <button
            class="flex h-7 items-center gap-1 rounded-lg px-2 text-xs transition-colors hover:bg-muted hover:text-foreground"
            title="帮我批准（免确认直接执行）"
            @click="toast.toast('帮我批准功能开发中', 'info')"
          >
            <svg class="h-3.5 w-3.5 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>自动批准</span>
          </button>

          <!-- 思考深度选择 -->
          <div v-if="sessions.current">
            <ReasoningPicker v-model="reasoningOverride" />
          </div>
        </div>

        <!-- 右侧模型切换与发送/停止 -->
        <div class="flex items-center gap-2">
          <ModelPicker
            v-if="sessions.current"
            :provider-id="sessions.current.providerId"
            :model-id="sessions.current.modelId"
            compact
            @change="(pid, mid) => sessions.update(sessions.current!.id, { providerId: pid, modelId: mid })"
          />

          <!-- 停止按钮 -->
          <button
            v-if="sessions.running"
            class="flex h-7.5 w-7.5 items-center justify-center rounded-xl bg-destructive text-destructive-foreground shadow-xs transition-transform hover:scale-105 active:scale-95"
            title="停止生成"
            @click="stop"
          >
            <svg class="h-3.5 w-3.5 fill-current" viewBox="0 0 24 24">
              <rect x="6" y="6" width="12" height="12" rx="2" />
            </svg>
          </button>

          <!-- 发送按钮 -->
          <button
            v-else
            :disabled="!input.trim()"
            :class="[
              'flex h-7.5 w-7.5 items-center justify-center rounded-xl transition-all duration-150',
              input.trim()
                ? 'bg-primary text-primary-foreground shadow-xs hover:opacity-90 active:scale-95'
                : 'bg-muted text-muted-foreground/40 cursor-not-allowed',
            ]"
            title="发送 (Enter)"
            @click="send"
          >
            <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 10.5L12 3m0 0l7.5 7.5M12 3v18" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
