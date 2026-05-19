<script setup lang="ts">
import { Send } from '@lucide/vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

defineProps<{
  busy: boolean
  modelValue: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'submit': []
}>()
</script>

<template>
  <form class="composer" @submit.prevent="emit('submit')">
    <textarea
      :value="modelValue"
      rows="3"
      :placeholder="t('chat.placeholder')"
      @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
    />
    <button class="primary-button" :disabled="busy || !modelValue.trim()">
      <Send :size="15" />
      <span>{{ t('chat.send') }}</span>
    </button>
  </form>
</template>
