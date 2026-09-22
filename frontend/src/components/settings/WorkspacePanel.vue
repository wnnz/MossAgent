<script setup lang="ts">
import { ref } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'

const settings = useSettingsStore()
const toast = useToastStore()

const workspacePath = ref(settings.getSetting('workspace_path'))
const maxTurns = ref(settings.getSetting('max_turns') || '25')

async function save() {
  try {
    await settings.updateSetting('workspace_path', workspacePath.value.trim())
    await settings.updateSetting('max_turns', maxTurns.value.trim() || '25')
    toast.toast('工作区设置已保存', 'success')
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  }
}
</script>

<template>
  <div class="max-w-lg space-y-4">
    <p class="text-sm text-muted-foreground">
      Agent 工作区目录：文件工具与命令执行被限制在该目录内（拒绝路径穿越）。留空使用默认目录（数据目录下 workspace）。
    </p>
    <div class="space-y-1.5">
      <Label>工作区目录</Label>
      <Input v-model="workspacePath" placeholder="例如 C:\work\my-project" />
    </div>
    <div class="space-y-1.5">
      <Label>最大轮次（Agent 循环上限）</Label>
      <Input v-model="maxTurns" type="number" />
    </div>
    <Button @click="save">保存</Button>
  </div>
</template>
