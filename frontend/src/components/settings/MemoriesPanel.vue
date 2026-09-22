<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useToastStore } from '@/stores/toastStore'
import { memoriesApi } from '@/api/memories'
import type { MemoryItem } from '@/types/domain'
import Button from '@/components/ui/Button.vue'
import Badge from '@/components/ui/Badge.vue'
import Dialog from '@/components/ui/Dialog.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Select from '@/components/ui/Select.vue'
import Textarea from '@/components/ui/Textarea.vue'

const toast = useToastStore()
const memories = ref<MemoryItem[]>([])
const searchQuery = ref('')
const dialogOpen = ref(false)
const editing = ref<MemoryItem | null>(null)

const title = ref('')
const content = ref('')
const tags = ref('')
const scope = ref<'global' | 'project'>('global')

async function reload() {
  memories.value = await memoriesApi.getAll()
}

onMounted(reload)

function openCreate() {
  editing.value = null
  title.value = content.value = tags.value = ''
  scope.value = 'global'
  dialogOpen.value = true
}

function openEdit(item: MemoryItem) {
  editing.value = item
  title.value = item.title
  content.value = item.content
  tags.value = item.tags ?? ''
  scope.value = item.scope
  dialogOpen.value = true
}

async function save() {
  if (!title.value.trim() || !content.value.trim()) {
    toast.toast('标题和内容不能为空', 'error')
    return
  }
  const data = { scope: scope.value, title: title.value.trim(), content: content.value, tags: tags.value || null }
  try {
    if (editing.value) {
      await memoriesApi.update(editing.value.id, data)
    } else {
      await memoriesApi.create(data)
    }
    toast.toast('记忆已保存', 'success')
    dialogOpen.value = false
    await reload()
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  }
}

async function remove(item: MemoryItem) {
  if (!confirm(`确认删除记忆「${item.title}」？`)) return
  try {
    await memoriesApi.remove(item.id)
    await reload()
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '删除失败', 'error')
  }
}

async function search() {
  memories.value = searchQuery.value.trim()
    ? await memoriesApi.search(searchQuery.value.trim())
    : await memoriesApi.getAll()
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between gap-2">
      <p class="text-sm text-muted-foreground">记忆（global/project 两级，系统提示词注入摘要）。</p>
      <Button size="sm" @click="openCreate">＋ 新增记忆</Button>
    </div>
    <div class="flex gap-2">
      <Input v-model="searchQuery" placeholder="按关键词检索记忆…" @keyup.enter="search" />
      <Button size="sm" variant="outline" @click="search">搜索</Button>
    </div>

    <div v-if="memories.length === 0" class="rounded-md border p-6 text-center text-sm text-muted-foreground">暂无记忆。</div>

    <div v-for="m in memories" :key="m.id" class="rounded-md border p-3">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="font-medium">{{ m.title }}</span>
          <Badge variant="outline">{{ m.scope }}</Badge>
          <Badge v-if="m.tags" variant="secondary">{{ m.tags }}</Badge>
        </div>
        <div class="flex gap-1">
          <Button size="sm" variant="ghost" @click="openEdit(m)">编辑</Button>
          <Button size="sm" variant="ghost" @click="remove(m)">删除</Button>
        </div>
      </div>
      <p class="mt-1 line-clamp-3 whitespace-pre-wrap text-xs text-muted-foreground">{{ m.content }}</p>
    </div>

    <Dialog :open="dialogOpen" :title="editing ? '编辑记忆' : '新增记忆'" @update:open="dialogOpen = $event">
      <form class="space-y-4" @submit.prevent="save">
        <div class="space-y-1.5">
          <Label>标题</Label>
          <Input v-model="title" />
        </div>
        <div class="space-y-1.5">
          <Label>内容</Label>
          <Textarea v-model="content" :rows="6" />
        </div>
        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <Label>作用域</Label>
            <Select v-model="scope">
              <option value="global">global</option>
              <option value="project">project</option>
            </Select>
          </div>
          <div class="space-y-1.5">
            <Label>标签（可选）</Label>
            <Input v-model="tags" placeholder="逗号分隔" />
          </div>
        </div>
        <Button type="submit">保存</Button>
      </form>
    </Dialog>
  </div>
</template>
