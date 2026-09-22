<script setup lang="ts">
const props = defineProps<{ modelValue: boolean; disabled?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: boolean): void }>()

function toggle() {
  if (!props.disabled) emit('update:modelValue', !props.modelValue)
}
</script>

<template>
  <button
    type="button"
    role="switch"
    :aria-checked="props.modelValue"
    :disabled="props.disabled"
    :class="[
      'inline-flex h-5 w-9 shrink-0 items-center rounded-full border transition-colors disabled:cursor-not-allowed disabled:opacity-50',
      props.modelValue ? 'bg-primary' : 'bg-input',
    ]"
    @click="toggle"
  >
    <span
      :class="[
        'block h-4 w-4 rounded-full bg-background shadow transition-transform',
        props.modelValue ? 'translate-x-4' : 'translate-x-0.5',
      ]"
    />
  </button>
</template>
