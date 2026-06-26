<script setup lang="ts">
import { computed, ref } from 'vue'
import { Home as HomeIcon, PanelRightClose, PanelRightOpen, Plus, X, type LucideIcon } from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import ApprovalTab from './ApprovalTab.vue'
import GitPanel from './GitPanel.vue'
import EventsTab from './EventsTab.vue'
import DoctorTab from './DoctorTab.vue'
import OrchestrationTab from './OrchestrationTab.vue'
import PanelHome from './PanelHome.vue'
import PreviewBrowserPanel from './PreviewBrowserPanel.vue'
import AgentActivityPanel from './AgentActivityPanel.vue'
import { usePanelTabs, type PanelType } from '../composables/usePanelTabs'
import { useResponsiveMode } from '../composables/useElementSize'
import type { ApprovalDto, EventEnvelope, DoctorReportDto, OrchestrationSnapshotDto, RuntimeReadinessReceiptDto, ToolExecutionTimelineItemDto } from '../api'
import type { AgentActivity, AgentState, ThinkingStep, ProgressEvent } from '@/composables/useAgentActivity'

const { t } = useI18n()

const collapsed = defineModel<boolean>('collapsed', { default: false })
const panelWidth = defineModel<number>('width', { default: 420 })

const isResizing = ref(false)

const props = defineProps<{
  approvals: ApprovalDto[]
  events: EventEnvelope[]
  doctor: DoctorReportDto | null
  readiness: RuntimeReadinessReceiptDto | null
  orchestration: OrchestrationSnapshotDto | null
  toolExecutions: ToolExecutionTimelineItemDto[]
  shellCommand: string
  busy: boolean
  selectedSessionId: string | null
  currentProjectPath: string | undefined
  /** Agent activity data for the agent panel */
  agentActivity: AgentActivity
  agentStates: Record<string, AgentState>
  thinkingSteps: ThinkingStep[]
  progressEvents: ProgressEvent[]
}>()

const emit = defineEmits<{
  'request-approval': []
  'decide-approval': [approval: ApprovalDto, decision: 'approved' | 'rejected']
  'approval-created': [approval: ApprovalDto]
  'update:shellCommand': [value: string]
}>()

const {
  tabs,
  activeTabId,
  openPanel,
  closeTab,
  selectTab,
  goHome,
} = usePanelTabs()

// ---- Responsive mode detection ----
const panelRef = ref<HTMLElement | null>(null)
const { mode: responsiveMode, isCompact } = useResponsiveMode(panelRef)

const panelClass = computed(() => ({
  collapsed,
  resizing: isResizing,
  'mode-compact': isCompact.value,
  'mode-ultra': responsiveMode.value === 'ultra',
}))

const pendingApprovalCount = computed(() =>
  props.approvals?.filter((a) => a.status === 'pending').length ?? 0,
)

function handleOpenPanel(type: PanelType, title: string, icon: LucideIcon) {
  openPanel(type, title, icon)
}

function handleCloseTab(id: string, event: MouseEvent) {
  event.stopPropagation()
  closeTab(id)
}

function startResize(event: MouseEvent) {
  isResizing.value = true
  const startX = event.clientX
  const startWidth = panelWidth.value

  function onMouseMove(e: MouseEvent) {
    const delta = startX - e.clientX
    const newWidth = Math.max(280, Math.min(760, startWidth + delta))
    panelWidth.value = newWidth
  }

  function onMouseUp() {
    isResizing.value = false
    document.removeEventListener('mousemove', onMouseMove)
    document.removeEventListener('mouseup', onMouseUp)
  }

  document.addEventListener('mousemove', onMouseMove)
  document.addEventListener('mouseup', onMouseUp)
}
</script>

<template>
  <aside
    ref="panelRef"
    class="float-panel browser-tabs-panel"
    :class="panelClass"
    :style="{ width: collapsed ? undefined : `${panelWidth}px` }"
  >
    <!-- Resize handle -->
    <div
      v-if="!collapsed"
      class="float-panel-resizer"
      @mousedown="startResize"
    />

    <template v-if="!collapsed">
      <!-- Browser-style tab bar -->
      <div class="browser-tab-bar">
        <button
          class="browser-tab browser-tab-home"
          :class="{ active: activeTabId === 'home' }"
          :title="t('context.homeTitle')"
          @click="goHome"
        >
          <HomeIcon :size="14" />
          <span class="browser-tab-label">{{ t('context.homeTabLabel') }}</span>
        </button>

        <button
          v-for="tab in tabs.filter((t) => t.type !== 'home')"
          :key="tab.id"
          class="browser-tab"
          :class="{ active: activeTabId === tab.id }"
          :title="tab.title"
          @click="selectTab(tab.id)"
        >
          <component :is="tab.icon" :size="14" class="browser-tab-icon" />
          <span class="browser-tab-label">{{ tab.title }}</span>
          <span
            class="browser-tab-close"
            @click="handleCloseTab(tab.id, $event)"
          >
            <X :size="11" />
          </span>
        </button>

        <button
          class="browser-tab-add"
          :title="t('context.homeNewTab')"
          @click="goHome"
        >
          <Plus :size="14" />
        </button>

        <button
          class="browser-tab-collapse"
          :title="t('app.collapse')"
          @click="collapsed = true"
        >
          <PanelRightClose :size="14" />
        </button>
      </div>

      <!-- Tab content area -->
      <div class="browser-tab-content">
        <!-- Home panel -->
        <div v-show="activeTabId === 'home'" class="browser-tab-pane">
          <PanelHome
            :pending-approval-count="pendingApprovalCount"
            :compact="isCompact"
            @open-panel="handleOpenPanel"
          />
        </div>

        <!-- Git panel -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'git')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <GitPanel
            :approvals="approvals"
            :current-project-path="currentProjectPath"
            :selected-session-id="selectedSessionId"
            @decide-approval="(a, d) => emit('decide-approval', a, d)"
            @approval-created="emit('approval-created', $event)"
          />
        </div>

        <!-- Approval panel -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'approval')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <ApprovalTab
            :approvals="approvals"
            :shell-command="shellCommand"
            :busy="busy"
            :selected-session-id="selectedSessionId"
            @request-approval="emit('request-approval')"
            @decide-approval="(a, d) => emit('decide-approval', a, d)"
            @update:shell-command="emit('update:shellCommand', $event)"
          />
        </div>

        <!-- Orchestration panel -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'orchestration')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <OrchestrationTab :snapshot="orchestration" :tool-executions="toolExecutions" />
        </div>

        <!-- Events panel -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'events')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <EventsTab :events="events" />
        </div>

        <!-- Doctor panel -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'doctor')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <DoctorTab :doctor="doctor" :readiness="readiness" />
        </div>

        <!-- Preview browser panels -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'preview')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <PreviewBrowserPanel :initial-url="(tab.state?.url as string) ?? ''" />
        </div>

        <!-- Agent activity panel -->
        <div
          v-for="tab in tabs.filter((t) => t.type === 'agent')"
          :key="tab.id"
          v-show="activeTabId === tab.id"
          class="browser-tab-pane"
        >
          <AgentActivityPanel
            :activity="agentActivity"
            :agent-states="agentStates"
            :thinking-steps="thinkingSteps"
            :progress-events="progressEvents"
            :orchestration="orchestration"
          />
        </div>
      </div>
    </template>

    <!-- Collapsed state -->
    <div v-else class="float-panel-collapsed-bar">
      <button
        class="float-panel-toggle-btn"
        :title="t('app.expand')"
        @click="collapsed = false"
      >
        <PanelRightOpen :size="16" />
      </button>
      <button
        class="float-panel-collapsed-icon"
        :class="{ active: activeTabId === 'home' }"
        :title="t('context.homeTitle')"
        @click="collapsed = false; goHome()"
      >
        <HomeIcon :size="18" />
      </button>
    </div>
  </aside>
</template>
