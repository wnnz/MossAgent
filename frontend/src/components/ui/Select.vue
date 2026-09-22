<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = defineProps<{ modelValue?: string | number; disabled?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()
</script>

<template>
  <select
    :value="props.modelValue"
    :disabled="props.disabled"
    :class="cn(
      'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
      $attrs.class ?? '',
    )"
    @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
  >
    <slot />
  </select>
</template>
