/**
 * Background management composable for TinadecCode Desktop
 * Handles background settings persistence, file selection, and DOM application
 */

import { useStorage } from '@vueuse/core'
import { ref, watch, type Ref } from 'vue'
import {
  type BackgroundSettings,
  type BackgroundType,
  DEFAULT_BACKGROUND_SETTINGS,
} from '../types/background'

// Storage key for background settings
const STORAGE_KEY = 'tinadec-background'

/**
 * Get stored background settings reference (lazy initialization)
 */
let stored: Ref<BackgroundSettings> | null = null

function getStoredBackground(): Ref<BackgroundSettings> {
  if (!stored) {
    stored = useStorage<BackgroundSettings>(STORAGE_KEY, { ...DEFAULT_BACKGROUND_SETTINGS })
  }
  return stored
}

/**
 * Apply background settings to DOM
 */
function applyBackgroundToDOM(settings: BackgroundSettings): void {
  const root = document.documentElement
  
  // Set background type attribute
  root.setAttribute('data-bg-type', settings.type)
  
  // Set background opacity and blur as CSS variables
  root.style.setProperty('--bg-custom-opacity', `${settings.opacity / 100}`)
  root.style.setProperty('--bg-custom-blur', `${settings.blur}px`)
  
  // Set background size, position, and repeat
  root.style.setProperty('--bg-custom-size', settings.size)
  root.style.setProperty('--bg-custom-position', settings.position)
  root.style.setProperty('--bg-custom-repeat', settings.repeat)
  
  // Set background source if applicable
  if (settings.type !== 'none' && settings.source) {
    root.style.setProperty('--bg-custom-source', `url('${settings.source}')`)
  } else {
    root.style.removeProperty('--bg-custom-source')
  }
}

/**
 * Select background file using Electron dialog
 */
async function selectBackgroundFile(type: BackgroundType): Promise<string | null> {
  // Check if Electron API is available
  const tinadec = (window as any).tinadec
  if (!tinadec?.selectBackgroundFile) {
    console.warn('Electron file dialog API not available')
    return null
  }
  
  try {
    const result = await tinadec.selectBackgroundFile(type)
    return result || null
  } catch (error) {
    console.error('Failed to select background file:', error)
    return null
  }
}

export function useBackground() {
  const backgroundSettings = getStoredBackground()
  const isApplying = ref(false)
  
  /**
   * Apply current background settings to DOM
   */
  function applyBackground(): void {
    isApplying.value = true
    applyBackgroundToDOM(backgroundSettings.value)
    isApplying.value = false
  }
  
  /**
   * Update background type
   */
  function setBackgroundType(type: BackgroundType): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      type,
      // Reset source when changing type
      source: type === 'none' ? '' : backgroundSettings.value.source,
    }
  }
  
  /**
   * Update background source (URL or file path)
   */
  function setBackgroundSource(source: string): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      source,
    }
  }
  
  /**
   * Update background opacity (0-100)
   */
  function setBackgroundOpacity(opacity: number): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      opacity: Math.max(0, Math.min(100, opacity)),
    }
  }
  
  /**
   * Update background blur (0-20px)
   */
  function setBackgroundBlur(blur: number): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      blur: Math.max(0, Math.min(20, blur)),
    }
  }
  
  /**
   * Update background size
   */
  function setBackgroundSize(size: 'cover' | 'contain' | 'auto'): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      size,
    }
  }
  
  /**
   * Update background position
   */
  function setBackgroundPosition(position: 'center' | 'top' | 'bottom' | 'left' | 'right'): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      position,
    }
  }
  
  /**
   * Update background repeat
   */
  function setBackgroundRepeat(repeat: 'no-repeat' | 'repeat' | 'repeat-x' | 'repeat-y'): void {
    backgroundSettings.value = {
      ...backgroundSettings.value,
      repeat,
    }
  }
  
  /**
   * Select file and update source
   */
  async function selectFile(): Promise<void> {
    const type = backgroundSettings.value.type
    if (type === 'none' || type === 'html') {
      return
    }
    
    const filePath = await selectBackgroundFile(type)
    if (filePath) {
      setBackgroundSource(filePath)
    }
  }
  
  /**
   * Reset background to default settings
   */
  function resetBackground(): void {
    backgroundSettings.value = { ...DEFAULT_BACKGROUND_SETTINGS }
  }
  
  // Watch for settings changes and apply to DOM
  watch(
    backgroundSettings,
    () => {
      applyBackground()
    },
    { deep: true, immediate: true }
  )
  
  return {
    // State
    settings: backgroundSettings,
    isApplying,
    
    // Methods
    applyBackground,
    setBackgroundType,
    setBackgroundSource,
    setBackgroundOpacity,
    setBackgroundBlur,
    setBackgroundSize,
    setBackgroundPosition,
    setBackgroundRepeat,
    selectFile,
    resetBackground,
  }
}
