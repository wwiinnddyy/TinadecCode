/**
 * Panel styles management composable for TinadecCode Desktop
 * Handles panel opacity, blur effects, and visual styling
 */

import { useStorage } from '@vueuse/core'
import { ref, watch, type Ref } from 'vue'
import {
  type PanelStyleSettings,
  DEFAULT_PANEL_STYLE_SETTINGS,
} from '../types/background'

// Storage key for panel styles
const STORAGE_KEY = 'tinadec-panel-styles'

export interface PanelStylesState {
  sidebar: PanelStyleSettings
  chatPanel: PanelStyleSettings
  contextPanel: PanelStyleSettings
}

const DEFAULT_PANEL_STYLES: PanelStylesState = {
  sidebar: { ...DEFAULT_PANEL_STYLE_SETTINGS },
  chatPanel: { ...DEFAULT_PANEL_STYLE_SETTINGS },
  contextPanel: { ...DEFAULT_PANEL_STYLE_SETTINGS },
}

/**
 * Get stored panel styles reference (lazy initialization)
 */
let stored: Ref<PanelStylesState> | null = null

function getStoredPanelStyles(): Ref<PanelStylesState> {
  if (!stored) {
    stored = useStorage<PanelStylesState>(STORAGE_KEY, { ...DEFAULT_PANEL_STYLES })
  }
  return stored
}

/**
 * Apply panel style settings to DOM element
 */
function applyPanelStyleToElement(
  element: HTMLElement,
  settings: PanelStyleSettings,
  panelName: string
): void {
  // Set data attributes for CSS targeting
  element.setAttribute('data-panel-effect', settings.effect)
  
  // Set CSS custom properties
  element.style.setProperty(`--panel-${panelName}-opacity`, `${settings.opacity / 100}`)
  element.style.setProperty(`--panel-${panelName}-blur`, `${settings.blur}px`)
  
  // Apply effect-specific styles
  switch (settings.effect) {
    case 'opaque':
      element.style.backdropFilter = 'none'
      element.style.backgroundColor = ''
      break
    case 'translucent':
      element.style.backdropFilter = 'none'
      element.style.backgroundColor = `rgba(var(--bg-primary-rgb, 10, 14, 20), ${settings.opacity / 100})`
      break
    case 'blur':
      element.style.backdropFilter = `blur(${settings.blur}px)`
      element.style.backgroundColor = `rgba(var(--bg-primary-rgb, 10, 14, 20), 0.8)`
      break
  }
}

/**
 * Compute panel style for Vue component binding
 */
function computePanelStyle(settings: PanelStyleSettings): Record<string, string> {
  const style: Record<string, string> = {}
  
  switch (settings.effect) {
    case 'opaque':
      // Default styling, no special effects
      break
    case 'translucent':
      style.backgroundColor = `rgba(var(--bg-primary-rgb, 10, 14, 20), ${settings.opacity / 100})`
      break
    case 'blur':
      style.backdropFilter = `blur(${settings.blur}px)`
      style.backgroundColor = `rgba(var(--bg-primary-rgb, 10, 14, 20), 0.8)`
      break
  }
  
  return style
}

export function usePanelStyles() {
  const panelStyles = getStoredPanelStyles()
  const isApplying = ref(false)
  
  /**
   * Update sidebar panel style
   */
  function updateSidebarStyle(settings: Partial<PanelStyleSettings>): void {
    panelStyles.value = {
      ...panelStyles.value,
      sidebar: {
        ...panelStyles.value.sidebar,
        ...settings,
      },
    }
  }
  
  /**
   * Update chat panel style
   */
  function updateChatPanelStyle(settings: Partial<PanelStyleSettings>): void {
    panelStyles.value = {
      ...panelStyles.value,
      chatPanel: {
        ...panelStyles.value.chatPanel,
        ...settings,
      },
    }
  }
  
  /**
   * Update context panel style
   */
  function updateContextPanelStyle(settings: Partial<PanelStyleSettings>): void {
    panelStyles.value = {
      ...panelStyles.value,
      contextPanel: {
        ...panelStyles.value.contextPanel,
        ...settings,
      },
    }
  }
  
  /**
   * Update panel style by name
   */
  function updatePanelStyle(
    panelName: 'sidebar' | 'chatPanel' | 'contextPanel',
    settings: Partial<PanelStyleSettings>
  ): void {
    switch (panelName) {
      case 'sidebar':
        updateSidebarStyle(settings)
        break
      case 'chatPanel':
        updateChatPanelStyle(settings)
        break
      case 'contextPanel':
        updateContextPanelStyle(settings)
        break
    }
  }
  
  /**
   * Reset all panel styles to default
   */
  function resetAllPanelStyles(): void {
    panelStyles.value = { ...DEFAULT_PANEL_STYLES }
  }
  
  /**
   * Reset specific panel style to default
   */
  function resetPanelStyle(panelName: 'sidebar' | 'chatPanel' | 'contextPanel'): void {
    panelStyles.value = {
      ...panelStyles.value,
      [panelName]: { ...DEFAULT_PANEL_STYLE_SETTINGS },
    }
  }
  
  /**
   * Get computed style for a panel (for Vue :style binding)
   */
  function getPanelStyle(panelName: 'sidebar' | 'chatPanel' | 'contextPanel'): Record<string, string> {
    return computePanelStyle(panelStyles.value[panelName])
  }
  
  /**
   * Get panel data attributes for CSS targeting
   */
  function getPanelDataAttributes(
    panelName: 'sidebar' | 'chatPanel' | 'contextPanel'
  ): Record<string, string> {
    const settings = panelStyles.value[panelName]
    return {
      'data-panel-effect': settings.effect,
    }
  }
  
  /**
   * Apply styles to DOM elements (for direct DOM manipulation)
   */
  function applyStylesToDOM(): void {
    isApplying.value = true
    
    const sidebarEl = document.querySelector('.sidebar')
    const chatPanelEl = document.querySelector('.conversation')
    const contextPanelEl = document.querySelector('.float-panel')
    
    if (sidebarEl) {
      applyPanelStyleToElement(sidebarEl as HTMLElement, panelStyles.value.sidebar, 'sidebar')
    }
    if (chatPanelEl) {
      applyPanelStyleToElement(chatPanelEl as HTMLElement, panelStyles.value.chatPanel, 'chat')
    }
    if (contextPanelEl) {
      applyPanelStyleToElement(contextPanelEl as HTMLElement, panelStyles.value.contextPanel, 'context')
    }
    
    isApplying.value = false
  }
  
  // Watch for settings changes and apply to DOM
  watch(
    panelStyles,
    () => {
      applyStylesToDOM()
    },
    { deep: true, immediate: true }
  )
  
  return {
    // State
    panelStyles,
    isApplying,
    
    // Methods
    updateSidebarStyle,
    updateChatPanelStyle,
    updateContextPanelStyle,
    updatePanelStyle,
    resetAllPanelStyles,
    resetPanelStyle,
    getPanelStyle,
    getPanelDataAttributes,
    applyStylesToDOM,
  }
}
