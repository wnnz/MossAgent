<script setup lang="ts">
import { computed, ref } from 'vue'
import { prettyJson } from '@/lib/utils'
import Badge from '@/components/ui/Badge.vue'

const props = defineProps<{
  call: {
    id: string
    name: string
    argumentsJson: string
    status: 'running' | 'finished' | 'error'
    result?: string
  }
}>()
const open = ref(false)
const args = computed(() => props.call.argumentsJson)
</script>

<template>
  <div class="my-1.5 overflow-hidden rounded-xl border border-border/70 bg-card/50 text-xs shadow-xs transition-colors">
    <!-- 卡片触发条 -->
    <button
      class="flex w-full items-center justify-between px-3 py-2 text-left transition-colors hover:bg-accent/40"
      @click="open = !open"
    >
      <div class="flex min-w-0 items-center gap-2">
        <!-- 状态指示图标 -->
        <div v-if="call.status === 'running'" class="flex h-4 w-4 items-center justify-center">
          <svg class="h-3.5 w-3.5 animate-spin text-primary" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
        </div>
        <div v-else-if="call.status === 'error'" class="flex h-4 w-4 items-center justify-center text-destructive">
          <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
          </svg>
        </div>
        <div v-else class="flex h-4 w-4 items-center justify-center text-emerald-500">
          <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
        </div>

        <!-- 工具名称 -->
        <span class="font-mono text-xs font-medium text-foreground">
          {{ call.name }}
        </span>

        <Badge v-if="call.status === 'running'" variant="secondary" class="bg-primary/10 text-primary text-[10px] px-1.5 py-0">运行中</Badge>
        <Badge v-else-if="call.status === 'error'" variant="destructive" class="text-[10px] px-1.5 py-0">失败</Badge>
        <Badge v-else variant="secondary" class="text-[10px] px-1.5 py-0">已完成</Badge>
      </div>

      <div class="flex items-center gap-1 text-muted-foreground">
        <span class="text-[10px]">{{ open ? '收起' : '详情' }}</span>
        <svg
          class="h-3.5 w-3.5 transition-transform duration-150"
          :class="open ? 'rotate-180' : ''"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          stroke-width="2"
        >
          <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </div>
    </button>

    <!-- 展开详情（参数与结果） -->
    <div v-if="open" class="space-y-2 border-t border-border/60 bg-muted/20 p-3">
      <div>
        <div class="mb-1 flex items-center justify-between text-[11px] font-medium text-muted-foreground">
          <span>调用参数 (Arguments)</span>
        </div>
        <pre class="max-h-48 overflow-x-auto rounded-lg border border-border/50 bg-muted/70 p-2 font-mono text-[11px] leading-relaxed text-foreground select-all">{{ prettyJson(args) || '（无参数）' }}</pre>
      </div>
      <div v-if="call.result !== undefined">
        <div class="mb-1 flex items-center justify-between text-[11px] font-medium text-muted-foreground">
          <span>返回结果 (Result)</span>
        </div>
        <pre class="max-h-64 overflow-auto rounded-lg border border-border/50 bg-muted/70 p-2 font-mono text-[11px] leading-relaxed text-foreground whitespace-pre-wrap select-all">{{ call.result || '（空）' }}</pre>
      </div>
    </div>
  </div>
</template>
