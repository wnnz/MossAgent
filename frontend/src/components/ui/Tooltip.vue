<script setup lang="ts">
const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()
</script>

<template>
  <div class="relative inline-block" @mouseleave="emit('update:open', false)">
    <slot />
    <div
      v-if="props.open"
      class="absolute bottom-full left-0 z-50 mb-1 whitespace-nowrap rounded-md border bg-popover px-2 py-1 text-xs text-popover-foreground shadow-md"
    >
      <slot name="content" />
    </div>
  </div>
</template>
