<script setup lang="ts">
import { onMounted } from 'vue'
import { useThemeStore } from '@/stores/themeStore'
import Toaster from '@/components/ui/Toaster.vue'

const themeStore = useThemeStore()
onMounted(() => themeStore.apply())
</script>

<template>
  <router-view />
  <Toaster />
</template>
