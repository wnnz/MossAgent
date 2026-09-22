<script setup lang="ts">
// 助手消息：Markdown 全宽渲染（marked + DOMPurify）
import { computed } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'

const props = defineProps<{ content: string }>()

const html = computed(() => {
  if (!props.content) return ''
  const raw = marked.parse(props.content, { async: false }) as string
  return DOMPurify.sanitize(raw)
})
</script>

<template>
  <div class="markdown-body text-sm" v-html="html" />
</template>
