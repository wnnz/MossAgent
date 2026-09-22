<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = withDefaults(defineProps<{ variant?: 'default' | 'secondary' | 'destructive' | 'outline' }>(), { variant: 'secondary' })

const variants: Record<string, string> = {
  default: 'bg-primary text-primary-foreground',
  secondary: 'bg-secondary text-secondary-foreground',
  destructive: 'bg-destructive text-destructive-foreground',
  outline: 'border border-input text-foreground',
}
</script>

<template>
  <span :class="cn('inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium', variants[props.variant])"><slot /></span>
</template>
