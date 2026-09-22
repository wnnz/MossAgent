<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = withDefaults(defineProps<{ tabs: Array<{ value: string; label: string }>; modelValue: string }>(), {})
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()
</script>

<template>
  <div :class="cn('inline-flex h-9 items-center justify-center rounded-lg bg-muted p-1 text-muted-foreground', $attrs.class ?? '')">
    <button
      v-for="tab in props.tabs"
      :key="tab.value"
      :class="cn(
        'inline-flex items-center justify-center whitespace-nowrap rounded-md px-3 py-1 text-sm font-medium transition-all',
        props.modelValue === tab.value ? 'bg-background text-foreground shadow' : 'hover:text-foreground',
      )"
      @click="emit('update:modelValue', tab.value)"
    >
      {{ tab.label }}
    </button>
  </div>
</template>
