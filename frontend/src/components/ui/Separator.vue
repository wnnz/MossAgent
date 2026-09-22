<script setup lang="ts">
import { cn } from '@/lib/utils'
</script>

<template>
  <div :class="cn('h-px w-full bg-border', $attrs.class ?? '')" />
</template>
