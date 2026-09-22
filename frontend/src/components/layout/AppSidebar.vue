<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/sessionStore'
import { useProjectStore } from '@/stores/projectStore'
import { useAuthStore } from '@/stores/authStore'
import { useThemeStore } from '@/stores/themeStore'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import { formatTime } from '@/lib/utils'
import type { Project } from '@/api/projects'
import Dialog from '@/components/ui/Dialog.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Button from '@/components/ui/Button.vue'

const router = useRouter()
const sessions = useSessionStore()
const projectStore = useProjectStore()
const auth = useAuthStore()
const theme = useThemeStore()
const settingsStore = useSettingsStore()
const toast = useToastStore()

// 项目展开状态（默认展开当前项目）
const expandedIds = ref<Set<number>>(new Set())
const showMoreProjects = ref(false)
const MAX_VISIBLE_PROJECTS = 8

const visibleProjects = computed(() =>
  showMoreProjects.value ? projectStore.projects : projectStore.projects.slice(0, MAX_VISIBLE_PROJECTS),
)
const hiddenCount = computed(() => Math.max(0, projectStore.projects.length - MAX_VISIBLE_PROJECTS))

function toggleProject(p: Project) {
  projectStore.setCurrent(p)
  if (expandedIds.value.has(p.id)) expandedIds.value.delete(p.id)
  else expandedIds.value.add(p.id)
}

function isExpanded(p: Project) {
  return expandedIds.value.has(p.id) || projectStore.current?.id === p.id
}

function projectSessions(p: Project) {
  return sessions.sessions.filter((s) => s.projectId === p.id)
}

const orphanSessions = computed(() =>
  sessions.sessions.filter((s) => s.projectId === null)
)

// 创建/导入项目
const projectDialogOpen = ref(false)
const projectName = ref('')
const projectPath = ref('')
const submitting = ref(false)

function openCreateProject() {
  projectName.value = projectPath.value = ''
  projectDialogOpen.value = true
}

async function submitProject() {
  if (!projectName.value.trim()) {
    toast.toast('项目名称不能为空', 'error')
    return
  }
  submitting.value = true
  try {
    const p = await projectStore.create(projectName.value.trim(), projectPath.value.trim() || undefined)
    expandedIds.value.add(p.id)
    toast.toast(projectPath.value.trim() ? `项目已导入：${p.name}` : `项目已创建：${p.name}`, 'success')
    projectDialogOpen.value = false
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '项目创建失败', 'error')
  } finally {
    submitting.value = false
  }
}

async function newTask() {
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
  const project = projectStore.current
  const workspacePath = project?.path || null
  await sessions.create(provider.id, modelId, 'off', workspacePath ? `新任务` : '新会话', workspacePath, project?.id ?? null)
  router.push('/')
}

async function openSession(s: { id: string }) {
  await sessions.select(s.id)
  router.push('/')
}

async function logout() {
  await auth.logout()
  router.push('/login')
}

function cycleTheme() {
  const modes = ['system', 'light', 'dark'] as const
  const nextIdx = (modes.indexOf(theme.mode) + 1) % modes.length
  theme.setMode(modes[nextIdx])
  toast.toast(`已切换至${modes[nextIdx] === 'light' ? '浅色' : modes[nextIdx] === 'dark' ? '深色' : '系统'}主题`, 'info')
}
</script>

<template>
  <aside class="flex h-full w-64 flex-col border-r border-border/70 bg-card/60 backdrop-blur-sm select-none">
    <!-- 品牌 Logo 栏 -->
    <div class="flex items-center justify-between px-3.5 pt-3.5 pb-2.5">
      <div class="flex items-center gap-2.5">
        <div class="relative flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10 shadow-xs">
          <img src="/favicon.svg" class="h-4 w-4" alt="logo" />
        </div>
        <div class="flex flex-col">
          <div class="flex items-center gap-1.5">
            <span class="text-xs font-semibold tracking-tight text-foreground">Coding Agent</span>
            <span class="h-1.5 w-1.5 rounded-full bg-emerald-500" title="在线" />
          </div>
          <span class="text-[10px] text-muted-foreground">AI 编程助手</span>
        </div>
      </div>
    </div>

    <!-- 顶部新对话操作 -->
    <div class="px-3 py-1.5">
      <button
        class="group flex w-full items-center justify-center gap-2 rounded-lg border border-border/80 bg-background/80 px-3 py-2 text-xs font-medium text-foreground shadow-xs transition-all duration-150 hover:border-primary/40 hover:bg-accent/60 hover:shadow-sm active:scale-[0.99]"
        @click="newTask"
      >
        <svg class="h-3.5 w-3.5 text-muted-foreground transition-colors group-hover:text-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
        </svg>
        <span>新建对话</span>
      </button>
    </div>

    <!-- 项目列表 -->
    <div class="mt-2 flex min-h-0 flex-1 flex-col">
      <div class="flex items-center justify-between px-3.5 py-1">
        <span class="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">项目</span>
        <button
          class="flex h-5 w-5 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
          title="创建或导入项目"
          @click="openCreateProject"
        >
          <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
        </button>
      </div>

      <div class="min-h-0 flex-1 overflow-y-auto px-2 py-1">
        <div v-if="projectStore.projects.length === 0" class="rounded-md border border-dashed border-border/60 px-3 py-4 text-center text-xs text-muted-foreground">
          <p class="mb-1">暂无项目</p>
          <button class="text-xs font-medium text-primary underline underline-offset-2 hover:opacity-80" @click="openCreateProject">
            点击新建或导入
          </button>
        </div>

        <!-- 各项目项 -->
        <div v-for="p in visibleProjects" :key="p.id" class="mb-1">
          <div
            :class="[
              'group flex w-full cursor-pointer items-center justify-between rounded-lg px-2 py-1.5 text-left text-xs transition-colors duration-150',
              projectStore.current?.id === p.id ? 'bg-accent/80 font-medium text-foreground' : 'text-muted-foreground hover:bg-accent/40 hover:text-foreground',
            ]"
            @click="toggleProject(p)"
          >
            <div class="flex min-w-0 items-center gap-1.5">
              <svg
                class="h-3 w-3 shrink-0 text-muted-foreground/80 transition-transform duration-150"
                :class="isExpanded(p) ? 'rotate-90 text-foreground' : ''"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                stroke-width="2"
              >
                <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
              </svg>
              <svg class="h-3.5 w-3.5 shrink-0 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
                <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-8.69-6.44l-2.12-2.12a1.5 1.5 0 00-1.061-.44H4.5A2.25 2.25 0 002.25 6v12a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9a2.25 2.25 0 00-2.25-2.25h-5.379a1.5 1.5 0 01-1.06-.44z" />
              </svg>
              <span class="truncate">{{ p.name }}</span>
            </div>
            <button
              class="flex h-4 w-4 shrink-0 items-center justify-center rounded text-muted-foreground/60 opacity-0 transition-all group-hover:opacity-100 hover:bg-destructive/10 hover:text-destructive"
              title="删除项目"
              @click.stop="projectStore.remove(p.id)"
            >
              <svg class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <!-- 项目下的会话列表 -->
          <div v-if="isExpanded(p)" class="mt-0.5 space-y-0.5 pl-4">
            <button
              v-for="s in projectSessions(p)"
              :key="s.id"
              :class="[
                'group flex w-full items-center justify-between rounded-md py-1 pl-2.5 pr-1.5 text-left text-xs transition-colors duration-150',
                sessions.current?.id === s.id
                  ? 'bg-primary/10 font-medium text-foreground'
                  : 'text-muted-foreground hover:bg-accent/40 hover:text-foreground',
              ]"
              @click="openSession(s)"
            >
              <div class="flex min-w-0 items-center gap-1.5">
                <span class="h-1 w-1 shrink-0 rounded-full" :class="sessions.current?.id === s.id ? 'bg-primary' : 'bg-muted-foreground/40'" />
                <span class="truncate">{{ s.title }}</span>
              </div>
              <div class="flex shrink-0 items-center gap-1">
                <span class="text-[10px] text-muted-foreground/70 opacity-0 transition-opacity group-hover:opacity-100">
                  {{ formatTime(s.updatedAt) }}
                </span>
                <span
                  class="flex h-4 w-4 items-center justify-center rounded text-muted-foreground/60 opacity-0 transition-all group-hover:opacity-100 hover:bg-destructive/10 hover:text-destructive"
                  title="删除对话"
                  @click.stop="sessions.remove(s.id)"
                >
                  <svg class="h-2.5 w-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </span>
              </div>
            </button>
            <p v-if="projectSessions(p).length === 0" class="py-1 pl-4 text-[11px] text-muted-foreground/60">无任务</p>
          </div>
        </div>

        <!-- 默认工作区（未归属项目的会话） -->
        <div v-if="orphanSessions.length > 0" class="mt-2 mb-1">
          <div class="flex items-center gap-1.5 px-2 py-1 text-[11px] font-medium text-muted-foreground">
            <svg class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-8.69-6.44l-2.12-2.12a1.5 1.5 0 00-1.061-.44H4.5A2.25 2.25 0 002.25 6v12a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9a2.25 2.25 0 00-2.25-2.25h-5.379a1.5 1.5 0 01-1.06-.44z" />
            </svg>
            <span>默认工作区</span>
          </div>
          <div class="space-y-0.5 pl-3">
            <button
              v-for="s in orphanSessions"
              :key="s.id"
              :class="[
                'group flex w-full items-center justify-between rounded-md py-1 pl-2.5 pr-1.5 text-left text-xs transition-colors duration-150',
                sessions.current?.id === s.id ? 'bg-primary/10 font-medium text-foreground' : 'text-muted-foreground hover:bg-accent/40 hover:text-foreground',
              ]"
              @click="openSession(s)"
            >
              <div class="flex min-w-0 items-center gap-1.5">
                <span class="h-1 w-1 shrink-0 rounded-full" :class="sessions.current?.id === s.id ? 'bg-primary' : 'bg-muted-foreground/40'" />
                <span class="truncate">{{ s.title }}</span>
              </div>
              <span
                class="flex h-4 w-4 shrink-0 items-center justify-center rounded text-muted-foreground/60 opacity-0 transition-all group-hover:opacity-100 hover:bg-destructive/10 hover:text-destructive"
                title="删除对话"
                @click.stop="sessions.remove(s.id)"
              >
                <svg class="h-2.5 w-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </span>
            </button>
          </div>
        </div>

        <button
          v-if="hiddenCount > 0"
          class="mt-1 w-full rounded px-2 py-1 text-left text-[11px] text-muted-foreground transition-colors hover:bg-accent/40 hover:text-foreground"
          @click="showMoreProjects = !showMoreProjects"
        >
          {{ showMoreProjects ? '收起显示' : `展开更多 (${hiddenCount})` }}
        </button>
      </div>
    </div>

    <!-- 底部控制栏：主题切换 + 设置 + 退出 -->
    <div class="flex items-center justify-between border-t border-border/70 p-2 text-muted-foreground">
      <button
        class="flex h-8 w-8 items-center justify-center rounded-lg transition-colors hover:bg-accent hover:text-foreground"
        :title="`主题: ${theme.mode === 'light' ? '浅色' : theme.mode === 'dark' ? '深色' : '跟随系统'} (点击切换)`"
        @click="cycleTheme"
      >
        <!-- 太阳图标 -->
        <svg v-if="theme.mode === 'light'" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v2.25m6.364.386l-1.591 1.591M21 12h-2.25m-.386 6.364l-1.591-1.591M12 18.75V21m-4.773-4.227l-1.591 1.591M5.25 12H3m4.227-4.773L5.636 5.636M15.75 12a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0z" />
        </svg>
        <!-- 月亮图标 -->
        <svg v-else-if="theme.mode === 'dark'" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21.752 15.002A9.718 9.718 0 0118 15.75c-5.385 0-9.75-4.365-9.75-9.75 0-1.33.266-2.597.748-3.752A9.753 9.753 0 003 11.25C3 16.635 7.365 21 12.75 21a9.753 9.753 0 009.002-5.998z" />
        </svg>
        <!-- 系统屏幕图标 -->
        <svg v-else class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 01-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0115 18.257V17.25m6-12V15a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 15V5.25m18 0A2.25 2.25 0 0018.75 3H5.25A2.25 2.25 0 003 5.25m18 0H3" />
        </svg>
      </button>

      <div class="flex items-center gap-1">
        <button
          class="flex h-8 w-8 items-center justify-center rounded-lg transition-colors hover:bg-accent hover:text-foreground"
          title="设置"
          @click="router.push('/settings')"
        >
          <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 011.37.49l1.296 2.247a1.125 1.125 0 01-.26 1.431l-1.003.827c-.293.24-.438.613-.431.992a6.759 6.759 0 010 .255c-.007.378.138.75.43.99l1.005.828c.424.35.534.954.26 1.43l-1.298 2.247a1.125 1.125 0 01-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.57 6.57 0 01-.22.128c-.331.183-.581.495-.644.869l-.213 1.28c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.02-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 01-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 01-1.369-.49l-1.297-2.247a1.125 1.125 0 01.26-1.431l1.004-.827c.292-.24.437-.613.43-.992a6.932 6.932 0 010-.255c.007-.378-.138-.75-.43-.99l-1.004-.828a1.125 1.125 0 01-.26-1.43l1.297-2.247a1.125 1.125 0 011.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.087.22-.128.332-.183.582-.495.644-.869l.214-1.281z" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </button>

        <button
          class="flex h-8 w-8 items-center justify-center rounded-lg transition-colors hover:bg-destructive/10 hover:text-destructive"
          title="退出登录"
          @click="logout"
        >
          <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 9V5.25A2.25 2.25 0 0013.5 3h-6a2.25 2.25 0 00-2.25 2.25v13.5A2.25 2.25 0 007.5 21h6a2.25 2.25 0 002.25-2.25V15M12 9l-3 3m0 0l3 3m-3-3h12.75" />
          </svg>
        </button>
      </div>
    </div>
  </aside>

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
      <p class="text-xs text-muted-foreground">
        粘贴已有目录即「导入项目」；仅填名称即「创建项目」（目录自动建立在默认工作区下）。
      </p>
      <div class="flex justify-end gap-2 pt-2">
        <Button variant="ghost" type="button" @click="projectDialogOpen = false">取消</Button>
        <Button type="submit" :disabled="submitting">{{ submitting ? '提交中…' : '确定' }}</Button>
      </div>
    </form>
  </Dialog>
</template>
