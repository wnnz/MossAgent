<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = withDefaults(defineProps<{
  variant?: 'default' | 'secondary' | 'destructive' | 'ghost' | 'outline'
  size?: 'default' | 'sm' | 'icon'
  type?: 'button' | 'submit'
  disabled?: boolean
}>(), { variant: 'default', size: 'default', type: 'button' })

const variantClasses: Record<string, string> = {
  default: 'bg-primary text-primary-foreground shadow-xs hover:bg-primary/90 active:scale-[0.98]',
  secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/80 active:scale-[0.98]',
  destructive: 'bg-destructive text-destructive-foreground hover:bg-destructive/90 active:scale-[0.98]',
  ghost: 'hover:bg-accent hover:text-accent-foreground',
  outline: 'border border-border/80 bg-background/50 hover:bg-accent hover:text-accent-foreground',
}
const sizeClasses: Record<string, string> = {
  default: 'h-9 px-4 py-2 text-sm',
  sm: 'h-8 px-3 text-xs',
  icon: 'h-9 w-9',
}
</script>

<template>
  <button
    :type="props.type"
    :disabled="props.disabled"
    :class="cn(
      'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg font-medium transition-all duration-150 disabled:pointer-events-none disabled:opacity-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring cursor-pointer',
      variantClasses[props.variant],
      sizeClasses[props.size],
      $attrs.class ?? '',
    )"
  >
    <slot />
  </button>
</template>
