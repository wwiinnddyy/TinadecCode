<script setup lang="ts">
import { ArrowUp, Plus, Image, FileText } from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import { ref } from 'vue'
import { UiButton, UiDropdownMenu } from '@/components/ui'

const { t } = useI18n()

const props = defineProps<{
  busy: boolean
  modelValue: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'submit': []
  'add-image': []
  'add-file': []
}>()

const textareaRef = ref<HTMLTextAreaElement | null>(null)
const showPlusMenu = ref(false)

function autoResize() {
  const el = textareaRef.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = Math.min(el.scrollHeight, 200) + 'px'
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    emit('submit')
  }
}
</script>

<template>
  <div class="composer">
    <div class="composer-box">
      <div class="composer-main">
        <div class="composer-plus-wrapper">
          <UiDropdownMenu v-model:open="showPlusMenu" class="plus-dropdown-menu">
            <template #trigger>
              <UiButton variant="ghost" size="icon" class="composer-plus">
                <Plus :size="16" />
              </UiButton>
            </template>
            <button class="plus-menu-item" @click="emit('add-image'); showPlusMenu = false">
              <Image :size="14" />
              <span>{{ t('chat.addImage') }}</span>
            </button>
            <button class="plus-menu-item" @click="emit('add-file'); showPlusMenu = false">
              <FileText :size="14" />
              <span>{{ t('chat.addFile') }}</span>
            </button>
          </UiDropdownMenu>
        </div>

        <textarea
          ref="textareaRef"
          :value="modelValue"
          class="composer-input"
          :placeholder="t('chat.placeholder')"
          rows="1"
          @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value); autoResize()"
          @keydown="handleKeydown"
        />

        <UiButton
          variant="ghost"
          size="icon"
          class="composer-send"
          :disabled="busy || !modelValue.trim()"
          @click="emit('submit')"
        >
          <ArrowUp :size="16" />
        </UiButton>
      </div>
    </div>
  </div>
</template>
