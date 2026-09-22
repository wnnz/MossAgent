<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useSettingsStore } from '@/stores/settingsStore'
import { useToastStore } from '@/stores/toastStore'
import { subAgentsApi } from '@/api/subagents'
import type { SubAgent } from '@/types/domain'
import Dialog from '@/components/ui/Dialog.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Select from '@/components/ui/Select.vue'
import Switch from '@/components/ui/Switch.vue'
import Textarea from '@/components/ui/Textarea.vue'

const props = defineProps<{ open: boolean; subAgent: SubAgent | null }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

const settings = useSettingsStore()
const toast = useToastStore()

const name = ref('')
const description = ref('')
const invocationRule = ref('')
const systemPrompt = ref('')
const providerId = ref('')
const modelId = ref('')
const reasoningEffort = ref<'off' | 'low' | 'medium' | 'high'>('off')
const maxTurns = ref(10)
const allowedToolsJson = ref('[]')
const enabled = ref(true)
const submitting = ref(false)

watch(() => [props.open, props.subAgent], () => {
  if (props.open) {
    name.value = props.subAgent?.name ?? ''
    description.value = props.subAgent?.description ?? ''
    invocationRule.value = props.subAgent?.invocationRule ?? ''
    systemPrompt.value = props.subAgent?.systemPrompt ?? ''
    providerId.value = props.subAgent ? String(props.subAgent.providerId) : ''
    modelId.value = props.subAgent?.modelId ?? ''
    reasoningEffort.value = props.subAgent?.reasoningEffort ?? 'off'
    maxTurns.value = props.subAgent?.maxTurns ?? 10
    allowedToolsJson.value = props.subAgent?.allowedToolsJson ?? '[]'
    enabled.value = props.subAgent?.enabled ?? true
  }
})

const provider = computed(() => settings.providers.find((p) => String(p.id) === providerId.value))
const title = computed(() => (props.subAgent ? '编辑子代理' : '新增子代理'))

function onProviderChange() {
  modelId.value = provider.value?.models[0]?.modelId ?? ''
}

async function submit() {
  if (!name.value.trim() || !providerId.value || !modelId.value.trim()) {
    toast.toast('name、provider、model 不能为空', 'error')
    return
  }
  try {
    JSON.parse(allowedToolsJson.value || '[]')
  } catch {
    toast.toast('允许工具列表必须是合法 JSON 数组', 'error')
    return
  }
  const data = {
    name: name.value.trim(),
    description: description.value.trim(),
    invocationRule: invocationRule.value.trim(),
    systemPrompt: systemPrompt.value,
    providerId: Number(providerId.value),
    modelId: modelId.value.trim(),
    reasoningEffort: reasoningEffort.value,
    maxTurns: maxTurns.value,
    allowedToolsJson: allowedToolsJson.value || '[]',
    enabled: enabled.value,
  }
  submitting.value = true
  try {
    if (props.subAgent) {
      await subAgentsApi.update(props.subAgent.id, data)
    } else {
      await subAgentsApi.create(data)
    }
    toast.toast('子代理已保存', 'success')
    emit('update:open', false)
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog :open="props.open" :title="title" wide @update:open="emit('update:open', $event)">
    <form class="space-y-4" @submit.prevent="submit">
      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1.5">
          <Label>名称</Label>
          <Input v-model="name" placeholder="例如 code-reviewer" />
        </div>
        <div class="space-y-1.5">
          <Label>最大轮次</Label>
          <Input v-model="maxTurns" type="number" />
        </div>
      </div>
      <div class="space-y-1.5">
        <Label>描述</Label>
        <Textarea v-model="description" :rows="2" placeholder="这个子代理做什么" />
      </div>
      <div class="space-y-1.5">
        <Label>调用规则（何时调用的自然语言规则，注入主代理系统提示词）</Label>
        <Textarea v-model="invocationRule" :rows="2" placeholder="例如：当代码修改完成需要审查时调用" />
      </div>
      <div class="space-y-1.5">
        <Label>系统提示词</Label>
        <Textarea v-model="systemPrompt" :rows="4" />
      </div>
      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1.5">
          <Label>提供商</Label>
          <Select v-model="providerId" @update:model-value="onProviderChange">
            <option value="">请选择</option>
            <option v-for="p in settings.providers.filter((p) => p.enabled)" :key="p.id" :value="String(p.id)">{{ p.name }}</option>
          </Select>
        </div>
        <div class="space-y-1.5">
          <Label>模型</Label>
          <Select v-model="modelId">
            <option value="">请选择</option>
            <option v-for="m in provider?.models ?? []" :key="m.modelId" :value="m.modelId">{{ m.displayName }}</option>
          </Select>
        </div>
      </div>
      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1.5">
          <Label>思考强度</Label>
          <Select v-model="reasoningEffort">
            <option value="off">off</option>
            <option value="low">low</option>
            <option value="medium">medium</option>
            <option value="high">high</option>
          </Select>
        </div>
        <div class="space-y-1.5">
          <Label>允许工具（JSON 数组，空 = 不限制）</Label>
          <Input v-model="allowedToolsJson" placeholder='["read_file","glob"]' />
        </div>
      </div>
      <div class="flex items-center gap-2">
        <Switch v-model="enabled" />
        <Label>启用</Label>
      </div>
      <Button type="submit" :disabled="submitting">{{ submitting ? '保存中…' : '保存' }}</Button>
    </form>
  </Dialog>
</template>
