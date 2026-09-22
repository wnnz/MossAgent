<script setup lang="ts">
// Composer 临时覆盖思考强度（精致胶囊选择器）
const model = defineModel<string>({ default: '' })

const options = [
  { value: '', label: '思考: 默认' },
  { value: 'off', label: '思考: 关闭' },
  { value: 'low', label: '思考: 深度低' },
  { value: 'medium', label: '思考: 深度中' },
  { value: 'high', label: '思考: 深度高' },
]
</script>

<template>
  <div class="relative inline-flex items-center">
    <select
      v-model="model"
      class="h-7 appearance-none rounded-lg border border-border/70 bg-muted/40 pl-2.5 pr-6 text-xs text-muted-foreground transition-colors hover:border-border hover:bg-muted/70 hover:text-foreground focus:outline-none focus:ring-1 focus:ring-ring cursor-pointer"
      title="调整本轮思考深度"
    >
      <option v-for="o in options" :key="o.value" :value="o.value" class="bg-popover text-popover-foreground">
        {{ o.label }}
      </option>
    </select>
    <svg class="pointer-events-none absolute right-1.5 h-3 w-3 text-muted-foreground/70" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
      <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
    </svg>
  </div>
</template>
