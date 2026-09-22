<script setup lang="ts">
import { cn } from '@/lib/utils'
</script>

<template>
  <div :class="cn('overflow-y-auto', $attrs.class ?? '')"><slot /></div>
</template>
