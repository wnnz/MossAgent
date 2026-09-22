<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useToastStore } from '@/stores/toastStore'
import { skillsApi } from '@/api/skills'
import type { Skill } from '@/types/domain'
import Button from '@/components/ui/Button.vue'
import Badge from '@/components/ui/Badge.vue'
import Dialog from '@/components/ui/Dialog.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Switch from '@/components/ui/Switch.vue'
import Textarea from '@/components/ui/Textarea.vue'

const toast = useToastStore()
const skills = ref<Skill[]>([])
const dialogOpen = ref(false)
const editing = ref<Skill | null>(null)

const name = ref('')
const description = ref('')
const instructions = ref('')
const enabled = ref(true)

async function reload() {
  skills.value = await skillsApi.getAll()
}

onMounted(reload)

function openCreate() {
  editing.value = null
  name.value = description.value = instructions.value = ''
  enabled.value = true
  dialogOpen.value = true
}

function openEdit(skill: Skill) {
  editing.value = skill
  name.value = skill.name
  description.value = skill.description
  instructions.value = skill.instructions
  enabled.value = skill.enabled
  dialogOpen.value = true
}

async function save() {
  if (!name.value.trim() || !instructions.value.trim()) {
    toast.toast('name 和 instructions 不能为空', 'error')
    return
  }
  const data = { name: name.value.trim(), description: description.value.trim(), instructions: instructions.value, enabled: enabled.value }
  try {
    if (editing.value) {
      await skillsApi.update(editing.value.id, data)
    } else {
      await skillsApi.create(data)
    }
    toast.toast('技能已保存', 'success')
    dialogOpen.value = false
    await reload()
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  }
}

async function toggle(skill: Skill) {
  try {
    await skillsApi.toggle(skill.id)
    await reload()
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '操作失败', 'error')
  }
}

async function remove(skill: Skill) {
  if (!confirm(`确认删除技能「${skill.name}」？`)) return
  try {
    await skillsApi.remove(skill.id)
    await reload()
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">技能：系统提示词只列索引，Agent 用 skill_load 按名加载正文。</p>
      <Button size="sm" @click="openCreate">＋ 新增技能</Button>
    </div>

    <div v-if="skills.length === 0" class="rounded-md border p-6 text-center text-sm text-muted-foreground">暂无技能。</div>

    <div v-for="s in skills" :key="s.id" class="rounded-md border p-3">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="font-medium">{{ s.name }}</span>
          <Badge :variant="s.enabled ? 'secondary' : 'destructive'">{{ s.enabled ? '已启用' : '已禁用' }}</Badge>
        </div>
        <div class="flex items-center gap-1">
          <Switch :model-value="s.enabled" @update:model-value="toggle(s)" />
          <Button size="sm" variant="ghost" @click="openEdit(s)">编辑</Button>
          <Button size="sm" variant="ghost" @click="remove(s)">删除</Button>
        </div>
      </div>
      <p class="mt-1 text-xs text-muted-foreground">{{ s.description }}</p>
    </div>

    <Dialog :open="dialogOpen" :title="editing ? '编辑技能' : '新增技能'" wide @update:open="dialogOpen = $event">
      <form class="space-y-4" @submit.prevent="save">
        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <Label>名称</Label>
            <Input v-model="name" placeholder="例如 commit-helper" />
          </div>
          <div class="space-y-1.5">
            <Label>描述</Label>
            <Input v-model="description" />
          </div>
        </div>
        <div class="space-y-1.5">
          <Label>正文（Markdown 指令）</Label>
          <Textarea v-model="instructions" :rows="10" />
        </div>
        <div class="flex items-center gap-2">
          <Switch v-model="enabled" />
          <Label>启用</Label>
        </div>
        <Button type="submit">保存</Button>
      </form>
    </Dialog>
  </div>
</template>
