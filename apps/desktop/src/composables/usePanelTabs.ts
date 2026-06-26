import { ref, computed } from 'vue'
import {
  GitBranch,
  ShieldCheck,
  Layers3,
  Activity,
  Stethoscope,
  Globe,
  Bot,
  type LucideIcon,
} from '@lucide/vue'

export type PanelType =
  | 'home'
  | 'git'
  | 'approval'
  | 'orchestration'
  | 'events'
  | 'doctor'
  | 'preview'
  | 'agent'

export interface PanelTab {
  id: string
  type: PanelType
  title: string
  icon: LucideIcon
  closable: boolean
  /** Optional per-tab state (e.g. preview URL) */
  state?: Record<string, unknown>
}

let tabCounter = 0
function generateTabId(): string {
  return `panel-tab-${++tabCounter}`
}

export function usePanelTabs() {
  const homeTab: PanelTab = {
    id: 'home',
    type: 'home',
    title: 'Home',
    icon: Globe,
    closable: false,
  }

  const tabs = ref<PanelTab[]>([{ ...homeTab }])
  const activeTabId = ref<string>('home')

  const activeTab = computed(
    () => tabs.value.find((t) => t.id === activeTabId.value) ?? tabs.value[0] ?? null,
  )

  const openTabs = computed(() => tabs.value.filter((t) => t.type !== 'home'))

  /**
   * Open a panel tab. For most types only one instance is allowed;
   * for 'preview' multiple instances are allowed.
   */
  function openPanel(
    type: PanelType,
    title: string,
    icon: LucideIcon,
    state?: Record<string, unknown>,
  ): string {
    // For non-preview types, reuse existing tab if present
    if (type !== 'preview') {
      const existing = tabs.value.find((t) => t.type === type)
      if (existing) {
        activeTabId.value = existing.id
        return existing.id
      }
    }

    const tab: PanelTab = {
      id: generateTabId(),
      type,
      title,
      icon,
      closable: true,
      state,
    }
    tabs.value = [...tabs.value, tab]
    activeTabId.value = tab.id
    return tab.id
  }

  function closeTab(id: string) {
    const tab = tabs.value.find((t) => t.id === id)
    if (!tab || !tab.closable) return

    const idx = tabs.value.findIndex((t) => t.id === id)
    tabs.value = tabs.value.filter((t) => t.id !== id)

    if (activeTabId.value === id) {
      // Switch to the previous tab, or next, or home
      const prevTab = tabs.value[idx - 1] ?? tabs.value[idx] ?? tabs.value[0]
      activeTabId.value = prevTab?.id ?? 'home'
    }
  }

  function selectTab(id: string) {
    if (tabs.value.some((t) => t.id === id)) {
      activeTabId.value = id
    }
  }

  function goHome() {
    activeTabId.value = 'home'
  }

  function updateTabState(id: string, state: Record<string, unknown>) {
    const tab = tabs.value.find((t) => t.id === id)
    if (tab) {
      tab.state = { ...tab.state, ...state }
    }
  }

  return {
    tabs,
    activeTabId,
    activeTab,
    openTabs,
    openPanel,
    closeTab,
    selectTab,
    goHome,
    updateTabState,
  }
}

/**
 * Panel type definitions for the home page grid.
 * Icons are imported here so both PanelHome and ContextPanel can use them.
 */
export const panelIcons = {
  GitBranch,
  ShieldCheck,
  Layers3,
  Activity,
  Stethoscope,
  Globe,
  Bot,
}
