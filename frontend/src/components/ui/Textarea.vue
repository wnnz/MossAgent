<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = defineProps<{ modelValue?: string; placeholder?: string; rows?: number; disabled?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()
</script>

<template>
  <textarea
    :value="props.modelValue"
    :placeholder="props.placeholder"
    :rows="props.rows ?? 3"
    :disabled="props.disabled"
    :class="cn(
      'flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
      $attrs.class ?? '',
    )"
    @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
  />
</template>
