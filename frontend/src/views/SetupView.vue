<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useToastStore } from '@/stores/toastStore'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'

const router = useRouter()
const auth = useAuthStore()
const toast = useToastStore()

const password = ref('')
const confirm = ref('')
const submitting = ref(false)

async function submit() {
  if (password.value.length < 6) {
    toast.toast('密码至少 6 位', 'error')
    return
  }
  if (password.value !== confirm.value) {
    toast.toast('两次输入的密码不一致', 'error')
    return
  }
  submitting.value = true
  try {
    await auth.setup(password.value)
    toast.toast('密码已设置并自动登录', 'success')
    router.push('/')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '设置失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center">
    <div class="w-full max-w-sm rounded-lg border bg-card p-6 shadow-sm">
      <h1 class="mb-1 text-lg font-semibold">初始化 Coding Agent</h1>
      <p class="mb-6 text-sm text-muted-foreground">首次访问：设置访问密码（仅允许设置一次），成功后自动登录。</p>
      <form class="space-y-4" @submit.prevent="submit">
        <div class="space-y-1.5">
          <Label>密码</Label>
          <Input v-model="password" type="password" placeholder="至少 6 位" />
        </div>
        <div class="space-y-1.5">
          <Label>确认密码</Label>
          <Input v-model="confirm" type="password" placeholder="再次输入" />
        </div>
        <Button type="submit" class="w-full" :disabled="submitting">{{ submitting ? '设置中…' : '设置密码' }}</Button>
      </form>
    </div>
  </div>
</template>
