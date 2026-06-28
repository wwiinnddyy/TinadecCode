/**
 * Panel style control component
 * Provides UI for adjusting panel opacity, blur, and effect type
 */

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PanelStyleSettings } from '@/types/background'

const { t } = useI18n()

const props = defineProps<{
  label: string
  settings: PanelStyleSettings
}>()

const emit = defineEmits<{
  update: [settings: Partial<PanelStyleSettings>]
}>()

// Effect options
const effectOptions = computed(() => [
  { value: 'opaque', label: t('settings.effectOpaque') },
  { value: 'translucent', label: t('settings.effectTranslucent') },
  { value: 'blur', label: t('settings.effectBlur') },
])

// Update effect type
function setEffect(effect: 'opaque' | 'translucent' | 'blur'): void {
  emit('update', { effect })
}

// Update opacity
function setOpacity(event: Event): void {
  const target = event.target as HTMLInputElement
  emit('update', { opacity: parseInt(target.value) })
}

// Update blur
function setBlur(event: Event): void {
  const target = event.target as HTMLInputElement
  emit('update', { blur: parseInt(target.value) })
}

// Compute preview style
const previewStyle = computed(() => {
  const style: Record<string, string> = {
    height: '60px',
    borderRadius: '6px',
    border: '1px solid var(--border-default)',
    background: 'var(--bg-primary)',
    transition: 'all 0.2s ease',
  }
  
  switch (props.settings.effect) {
    case 'opaque':
      break
    case 'translucent':
      style.backgroundColor = `rgba(var(--bg-primary-rgb, 10, 14, 20), ${props.settings.opacity / 100})`
      break
    case 'blur':
      style.backdropFilter = `blur(${props.settings.blur}px)`
      style.backgroundColor = `rgba(var(--bg-primary-rgb, 10, 14, 20), 0.8)`
      break
  }
  
  return style
})

// Check if opacity slider should be shown
const showOpacitySlider = computed(() => {
  return props.settings.effect === 'translucent'
})

// Check if blur slider should be shown
const showBlurSlider = computed(() => {
  return props.settings.effect === 'blur'
})
</script>

<template>
  <div class="panel-style-control">
    <!-- Header -->
    <div class="control-header">
      <span class="control-label">{{ label }}</span>
      <button
        class="control-reset"
        :title="t('settings.reset')"
        @click="emit('update', { opacity: 100, blur: 0, effect: 'opaque' })"
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
          <path d="M3 3v5h5" />
        </svg>
      </button>
    </div>
    
    <!-- Effect selector -->
    <div class="effect-selector">
      <button
        v-for="option in effectOptions"
        :key="option.value"
        :class="['effect-option', { active: settings.effect === option.value }]"
        @click="setEffect(option.value as any)"
      >
        {{ option.label }}
      </button>
    </div>
    
    <!-- Opacity slider (for translucent effect) -->
    <div v-if="showOpacitySlider" class="slider-row">
      <label class="slider-label">{{ t('settings.opacity') }}</label>
      <input
        type="range"
        min="0"
        max="100"
        :value="settings.opacity"
        class="slider-input"
        @input="setOpacity"
      />
      <span class="slider-value">{{ settings.opacity }}%</span>
    </div>
    
    <!-- Blur slider (for blur effect) -->
    <div v-if="showBlurSlider" class="slider-row">
      <label class="slider-label">{{ t('settings.blur') }}</label>
      <input
        type="range"
        min="0"
        max="20"
        :value="settings.blur"
        class="slider-input"
        @input="setBlur"
      />
      <span class="slider-value">{{ settings.blur }}px</span>
    </div>
    
    <!-- Preview -->
    <div class="preview-container">
      <div class="preview-background">
        <div class="preview-pattern" />
      </div>
      <div :style="previewStyle" class="preview-panel">
        <span class="preview-text">{{ t('settings.preview') }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.panel-style-control {
  padding: 12px;
  border: 1px solid var(--border-default);
  border-radius: 8px;
  background: var(--bg-secondary);
}

.control-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.control-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}

.control-reset {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  color: var(--text-muted);
  background: transparent;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: color 0.1s, background 0.1s;
}

.control-reset:hover {
  color: var(--text-primary);
  background: var(--bg-hover);
}

.effect-selector {
  display: flex;
  gap: 4px;
  margin-bottom: 12px;
}

.effect-option {
  flex: 1;
  padding: 6px 8px;
  font-size: 11px;
  font-weight: 500;
  color: var(--text-secondary);
  background: var(--bg-tertiary);
  border: 1px solid var(--border-default);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.1s;
}

.effect-option:hover {
  color: var(--text-primary);
  background: var(--bg-hover);
}

.effect-option.active {
  color: var(--accent-primary);
  background: var(--accent-soft);
  border-color: var(--accent-primary);
}

.slider-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.slider-label {
  min-width: 60px;
  font-size: 12px;
  color: var(--text-secondary);
}

.slider-input {
  flex: 1;
  height: 4px;
  -webkit-appearance: none;
  appearance: none;
  background: var(--bg-tertiary);
  border-radius: 2px;
  outline: none;
}

.slider-input::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 14px;
  height: 14px;
  background: var(--accent-primary);
  border-radius: 50%;
  cursor: pointer;
  transition: transform 0.1s;
}

.slider-input::-webkit-slider-thumb:hover {
  transform: scale(1.2);
}

.slider-value {
  min-width: 40px;
  font-size: 12px;
  color: var(--text-muted);
  text-align: right;
}

.preview-container {
  position: relative;
  height: 60px;
  margin-top: 12px;
  border-radius: 6px;
  overflow: hidden;
}

.preview-background {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
}

.preview-pattern {
  width: 100%;
  height: 100%;
  background: 
    repeating-linear-gradient(
      45deg,
      var(--accent-soft),
      var(--accent-soft) 10px,
      transparent 10px,
      transparent 20px
    );
}

.preview-panel {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.preview-text {
  font-size: 12px;
  color: var(--text-muted);
}
</style>
