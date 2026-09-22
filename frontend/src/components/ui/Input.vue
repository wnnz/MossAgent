<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = defineProps<{ modelValue?: string | number; placeholder?: string; type?: string; disabled?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()
</script>

<template>
  <input
    :type="props.type ?? 'text'"
    :value="props.modelValue"
    :placeholder="props.placeholder"
    :disabled="props.disabled"
    :class="cn(
      'flex h-9 w-full rounded-lg border border-input bg-transparent px-3 py-1.5 text-sm shadow-xs transition-colors placeholder:text-muted-foreground/60 focus-visible:outline-none focus-visible:ring-1.5 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
      $attrs.class ?? '',
    )"
    @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
  />
</template>
