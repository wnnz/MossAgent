<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useToastStore } from '@/stores/toastStore'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Switch from '@/components/ui/Switch.vue'

const router = useRouter()
const auth = useAuthStore()
const toast = useToastStore()

const password = ref('')
const rememberMe = ref(false)
const submitting = ref(false)

async function submit() {
  submitting.value = true
  try {
    await auth.login(password.value, rememberMe.value)
    router.push('/')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '登录失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center">
    <div class="w-full max-w-sm rounded-lg border bg-card p-6 shadow-sm">
      <h1 class="mb-1 text-lg font-semibold">登录 Coding Agent</h1>
      <p class="mb-6 text-sm text-muted-foreground">输入访问密码继续。</p>
      <form class="space-y-4" @submit.prevent="submit">
        <div class="space-y-1.5">
          <Label>密码</Label>
          <Input v-model="password" type="password" placeholder="访问密码" />
        </div>
        <div class="flex items-center gap-2">
          <Switch v-model="rememberMe" />
          <Label>记住我（30 天）</Label>
        </div>
        <Button type="submit" class="w-full" :disabled="submitting">{{ submitting ? '登录中…' : '登录' }}</Button>
      </form>
    </div>
  </div>
</template>
