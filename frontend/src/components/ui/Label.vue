<script setup lang="ts">
import { cn } from '@/lib/utils'
</script>

<template>
  <label :class="cn('text-sm font-medium leading-none', $attrs.class ?? '')"><slot /></label>
</template>
