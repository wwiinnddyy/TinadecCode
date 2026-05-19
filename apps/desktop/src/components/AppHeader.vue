<script setup lang="ts">
import { Diamond, Minus, Square, X } from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import { UiButton } from '@/components/ui'

const { t } = useI18n()

defineProps<{
  busy: boolean
}>()

function minimizeWindow() {
  window.tinadec?.minimizeWindow?.()
}

function maximizeWindow() {
  window.tinadec?.maximizeWindow?.()
}

function closeWindow() {
  window.tinadec?.closeWindow?.()
}
</script>

<template>
  <header class="topbar">
    <div class="brand">
      <Diamond :size="18" />
      <span>Tinadec</span>
    </div>

    <div class="window-controls">
      <UiButton variant="ghost" size="icon" class="window-btn minimize" :title="t('app.minimize')" @click="minimizeWindow">
        <Minus :size="14" />
      </UiButton>
      <UiButton variant="ghost" size="icon" class="window-btn maximize" :title="t('app.maximize')" @click="maximizeWindow">
        <Square :size="12" />
      </UiButton>
      <UiButton variant="ghost" size="icon" class="window-btn close" :title="t('app.close')" @click="closeWindow">
        <X :size="14" />
      </UiButton>
    </div>
  </header>
</template>
