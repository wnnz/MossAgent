<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useToastStore } from '@/stores/toastStore'
import { providersApi } from '@/api/providers'
import type { Provider, ProviderModel } from '@/types/domain'
import Dialog from '@/components/ui/Dialog.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Label from '@/components/ui/Label.vue'
import Switch from '@/components/ui/Switch.vue'

const props = defineProps<{ open: boolean; provider: Provider | null; model: ProviderModel | null }>()
const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

const toast = useToastStore()

const modelId = ref('')
const displayName = ref('')
const supportsTools = ref(true)
const supportsReasoning = ref(false)
const defaultReasoningEffort = ref<'off' | 'low' | 'medium' | 'high'>('off')
const maxContextTokens = ref(128000)
const maxOutputTokens = ref(8192)
const isCustom = ref(true)
const submitting = ref(false)

watch(() => [props.open, props.model], () => {
  if (props.open && props.provider) {
    modelId.value = props.model?.modelId ?? ''
    displayName.value = props.model?.displayName ?? ''
    supportsTools.value = props.model?.supportsTools ?? true
    supportsReasoning.value = props.model?.supportsReasoning ?? false
    defaultReasoningEffort.value = props.model?.defaultReasoningEffort ?? 'off'
    maxContextTokens.value = props.model?.maxContextTokens ?? 128000
    maxOutputTokens.value = props.model?.maxOutputTokens ?? 8192
    isCustom.value = props.model?.isCustom ?? true
  }
})

const title = computed(() => (props.model ? '编辑模型' : '添加模型'))

async function submit() {
  if (!props.provider || !modelId.value.trim()) {
    toast.toast('modelId 不能为空', 'error')
    return
  }
  const data = {
    modelId: modelId.value.trim(),
    displayName: displayName.value.trim() || modelId.value.trim(),
    supportsTools: supportsTools.value,
    supportsReasoning: supportsReasoning.value,
    defaultReasoningEffort: defaultReasoningEffort.value,
    maxContextTokens: maxContextTokens.value,
    maxOutputTokens: maxOutputTokens.value,
    isCustom: isCustom.value,
  }
  submitting.value = true
  try {
    if (props.model) {
      await providersApi.updateModel(props.provider.id, props.model.id, data)
    } else {
      await providersApi.addModel(props.provider.id, data)
    }
    toast.toast('模型已保存', 'success')
    emit('update:open', false)
  } catch (e) {
    toast.toast(e instanceof Error ? e.message : '保存失败', 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog :open="props.open" :title="title" @update:open="emit('update:open', $event)">
    <form class="space-y-4" @submit.prevent="submit">
      <div class="space-y-1.5">
        <Label>模型 ID</Label>
        <Input v-model="modelId" placeholder="例如 deepseek-chat / claude-sonnet-4-5" />
      </div>
      <div class="space-y-1.5">
        <Label>显示名</Label>
        <Input v-model="displayName" />
      </div>
      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1.5">
          <Label>上下文长度（tokens）</Label>
          <Input v-model="maxContextTokens" type="number" />
        </div>
        <div class="space-y-1.5">
          <Label>最大输出（tokens）</Label>
          <Input v-model="maxOutputTokens" type="number" />
        </div>
      </div>
      <div class="space-y-1.5">
        <Label>默认思考强度</Label>
        <Select v-model="defaultReasoningEffort">
          <option value="off">off</option>
          <option value="low">low</option>
          <option value="medium">medium</option>
          <option value="high">high</option>
        </Select>
      </div>
      <div class="flex flex-col gap-2">
        <div class="flex items-center gap-2"><Switch v-model="supportsTools" /><Label>支持工具调用</Label></div>
        <div class="flex items-center gap-2"><Switch v-model="supportsReasoning" /><Label>支持推理</Label></div>
      </div>
      <Button type="submit" :disabled="submitting">{{ submitting ? '保存中…' : '保存' }}</Button>
    </form>
  </Dialog>
</template>
