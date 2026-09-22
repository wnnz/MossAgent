<script setup lang="ts">
import { computed, ref, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/sessionStore'
import { useProjectStore } from '@/stores/projectStore'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import UserMessage from '@/components/chat/UserMessage.vue'
import AssistantMessage from '@/components/chat/AssistantMessage.vue'
import ToolCallCard from '@/components/chat/ToolCallCard.vue'
import Dialog from '@/components/ui/Dialog.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Button from '@/components/ui/Button.vue'

const router = useRouter()
const sessions = useSessionStore()
const projectStore = useProjectStore()
const settingsStore = useSettingsStore()
const toast = useToastStore()
const bottomAnchor = ref<HTMLElement | null>(null)

function scrollToBottom(smooth = true) {
  nextTick(() => {
    bottomAnchor.value?.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto' })
  })
}

// 流式内容更新或新消息到达时自动滚动到底部
watch(() => sessions.streaming?.content, () => scrollToBottom(true))
watch(() => sessions.streaming?.toolCalls.length, () => scrollToBottom(true))
watch(() => sessions.messages.length, () => scrollToBottom(false))

// 会话归属项目（当前会话的项目优先，否则当前项目）
const project = computed(() => {
  const pid = sessions.current?.projectId
  if (pid != null) {
    const p = projectStore.projects.find((x) => x.id === pid)
    if (p) return p
  }
  return projectStore.current
})

// 创建/导入项目（空态快捷方式）
const projectDialogOpen = ref(false)
const projectName = ref('')
const projectPath = ref('')
const submitting = ref(false)

async function submitProject() {
  if (!projectName.value.trim()) return
  submitting.value = true
  try {
    const p = await projectStore.create(projectName.value.trim(), projectPath.value.trim() || undefined)
    toast.toast(projectPath.value.trim() ? `项目已导入：${p.name}` : `项目已创建：${p.name}`, 'success')
    projectDialogOpen.value = false
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '项目创建失败', 'error')
  } finally {
    submitting.value = false
  }
}

// 快速启动推荐 Prompt 卡片
const promptSuggestions = [
  {
    title: '界面美化与重构',
    desc: '优化整体前端样式与组件，实现现代极简设计',
    prompt: '帮我重构前端，让界面简约、清新、美观',
    icon: 'palette',
  },
  {
    title: '代码审查与分析',
    desc: '审查项目关键代码，排查潜在隐患与可维护性',
    prompt: '对当前项目代码进行静态审查，找出高风险逻辑缺陷与改进点',
    icon: 'shield-check',
  },
  {
    title: '编写测试用例',
    desc: '补充自动化单元测试与集成测试覆盖',
    prompt: '为当前项目的主要业务功能编写完备的自动化测试用例',
    icon: 'flask',
  },
  {
    title: '架构梳理与优化',
    desc: '分析工程依赖链路与分层结构设计',
    prompt: '梳理本项目的整体架构与调用流程，给出系统级优化建议',
    icon: 'git-branch',
  },
]

async function pickPrompt(text: string) {
  if (!sessions.current) {
    const enabledProviders = settingsStore.providers.filter((p) => p.enabled)
    if (enabledProviders.length === 0) {
      router.push('/settings')
      return
    }
    const provider = enabledProviders[0]
    const modelId = provider.models[0]?.modelId ?? ''
    if (!modelId) {
      router.push('/settings')
      return
    }
    const p = projectStore.current
    const workspacePath = p?.path || null
    await sessions.create(provider.id, modelId, 'off', workspacePath ? '新任务' : '新会话', workspacePath, p?.id ?? null)
  }
  await sessions.send(text, null)
}
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <!-- 消息流：当有会话且至少有 1 条消息或正在流式渲染时展示 -->
    <div
      v-if="sessions.current && (sessions.messages.length > 0 || sessions.streaming)"
      class="min-h-0 flex-1 overflow-y-auto px-4 py-6"
    >
      <div class="mx-auto flex max-w-3xl flex-col gap-5">
        <template v-for="message in sessions.messages" :key="message.id">
          <UserMessage v-if="message.role === 'user'" :content="message.content" />
          <AssistantMessage v-else-if="message.role === 'assistant'" :content="message.content" />
        </template>

        <!-- 流式渲染中的助手回复与工具调用 -->
        <div v-if="sessions.streaming" class="flex flex-col gap-2.5">
          <div v-for="call in sessions.streaming.toolCalls" :key="call.id">
            <ToolCallCard :call="call" />
          </div>
          <AssistantMessage v-if="sessions.streaming.content" :content="sessions.streaming.content" />
          <div v-else class="flex items-center gap-2 py-2 text-xs text-muted-foreground">
            <div class="flex items-center gap-1">
              <span class="h-1.5 w-1.5 animate-bounce rounded-full bg-primary/60" style="animation-delay: 0ms;" />
              <span class="h-1.5 w-1.5 animate-bounce rounded-full bg-primary/60" style="animation-delay: 150ms;" />
              <span class="h-1.5 w-1.5 animate-bounce rounded-full bg-primary/60" style="animation-delay: 300ms;" />
            </div>
            <span class="tracking-wide">AI 正在深度思考中…</span>
          </div>
        </div>

        <div ref="bottomAnchor" />
      </div>
    </div>

    <!-- 空态：极简现代欢迎面板（居中 Logo + 项目胶囊 + 推荐 Prompt 卡片） -->
    <div v-else class="flex flex-1 flex-col items-center justify-center px-4 py-8 select-none">
      <div class="mx-auto flex w-full max-w-2xl flex-col items-center text-center">
        <!-- 微发光 Logo 容器 -->
        <div class="relative mb-5 flex h-14 w-14 items-center justify-center rounded-2xl border border-border/80 bg-card/80 shadow-md shadow-black/[0.04] backdrop-blur-sm">
          <img src="/favicon.svg" class="h-8 w-8" alt="logo" />
        </div>

        <!-- 标题 -->
        <h2 class="text-xl font-semibold tracking-tight text-foreground sm:text-2xl">
          你想在
          <button
            class="inline-flex items-center gap-1 rounded-lg border border-border/60 bg-muted/40 px-2 py-0.5 text-base font-medium text-foreground transition-colors hover:border-primary/40 hover:bg-muted"
            title="点击切换或创建项目"
            @click="projectDialogOpen = true"
          >
            <svg class="h-4 w-4 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-8.69-6.44l-2.12-2.12a1.5 1.5 0 00-1.061-.44H4.5A2.25 2.25 0 002.25 6v12a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9a2.25 2.25 0 00-2.25-2.25h-5.379a1.5 1.5 0 01-1.06-.44z" />
            </svg>
            <span>{{ project?.name ?? '默认工作区' }}</span>
          </button>
          中构建什么？
        </h2>

        <p class="mt-2 text-xs text-muted-foreground">
          输入任何工程需求，Coding Agent 将自主读写文件、运行终端、执行子代理协作。
        </p>

        <!-- 4 组推荐 Prompt 卡片 -->
        <div class="mt-8 grid w-full grid-cols-1 gap-3 sm:grid-cols-2">
          <button
            v-for="item in promptSuggestions"
            :key="item.title"
            class="group flex flex-col rounded-xl border border-border/70 bg-card/60 p-3.5 text-left shadow-xs backdrop-blur-xs transition-all duration-150 hover:border-primary/40 hover:bg-accent/50 hover:shadow-sm active:scale-[0.99]"
            @click="pickPrompt(item.prompt)"
          >
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-foreground group-hover:text-primary">{{ item.title }}</span>
              <svg class="h-3.5 w-3.5 text-muted-foreground/60 transition-transform group-hover:translate-x-0.5 group-hover:text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 19.5l15-15m0 0H8.25m11.25 0v11.25" />
              </svg>
            </div>
            <p class="mt-1 text-[11px] text-muted-foreground">{{ item.desc }}</p>
          </button>
        </div>
      </div>
    </div>

    <!-- 创建/导入项目 Dialog -->
    <Dialog :open="projectDialogOpen" title="创建或导入项目" @update:open="projectDialogOpen = $event">
      <form class="space-y-4" @submit.prevent="submitProject">
        <div class="space-y-1.5">
          <Label>项目名称（必填）</Label>
          <Input v-model="projectName" placeholder="例如 my-project" />
        </div>
        <div class="space-y-1.5">
          <Label>项目路径（选填）</Label>
          <Input v-model="projectPath" placeholder="例如 C:\work\my-project；留空则在默认工作区下创建同名目录" />
        </div>
        <div class="flex justify-end gap-2 pt-2">
          <Button variant="ghost" type="button" @click="projectDialogOpen = false">取消</Button>
          <Button type="submit" :disabled="submitting">{{ submitting ? '提交中…' : '确定' }}</Button>
        </div>
      </form>
    </Dialog>
  </div>
</template>
