<script setup lang="ts">
// 用户消息气泡（极简圆润设计）
defineProps<{ content: string }>()
</script>

<template>
  <div class="flex justify-end">
    <div class="max-w-[82%] rounded-2xl rounded-br-xs bg-primary px-4 py-2.5 text-sm leading-relaxed text-primary-foreground shadow-xs whitespace-pre-wrap selection:bg-primary-foreground selection:text-primary">
      {{ content }}
    </div>
  </div>
</template>
