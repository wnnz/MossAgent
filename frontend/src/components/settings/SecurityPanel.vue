<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '@/stores/authStore'
import { useToastStore } from '@/stores/toastStore'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'

const auth = useAuthStore()
const toast = useToastStore()

const currentPassword = ref('')
const newPassword = ref('')
const confirm = ref('')
const submitting = ref(false)

async function submit() {
  if (newPassword.value.length < 6) {
    toast.toast('新密码至少 6 位', 'error')
    return
  }
  if (newPassword.value !== confirm.value) {
    toast.toast('两次输入的新密码不一致', 'error')
    return
  }
  submitting.value = true
  try {
    await auth.changePassword(currentPassword.value, newPassword.value)
    toast.toast('密码已更新', 'success')
    currentPassword.value = newPassword.value = confirm.value = ''
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '修改失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <form class="max-w-sm space-y-4" @submit.prevent="submit">
    <div class="space-y-1.5">
      <Label>当前密码</Label>
      <Input v-model="currentPassword" type="password" />
    </div>
    <div class="space-y-1.5">
      <Label>新密码</Label>
      <Input v-model="newPassword" type="password" placeholder="至少 6 位" />
    </div>
    <div class="space-y-1.5">
      <Label>确认新密码</Label>
      <Input v-model="confirm" type="password" />
    </div>
    <Button type="submit" :disabled="submitting">{{ submitting ? '提交中…' : '修改密码' }}</Button>
  </form>
</template>
