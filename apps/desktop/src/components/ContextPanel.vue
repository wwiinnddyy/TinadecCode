<script setup lang="ts">
import { ref } from 'vue'
import { ChevronLeft, ChevronRight } from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import ApprovalTab from './ApprovalTab.vue'
import DiffTab from './DiffTab.vue'
import EventsTab from './EventsTab.vue'
import DoctorTab from './DoctorTab.vue'
import OrchestrationTab from './OrchestrationTab.vue'
import type { ApprovalDto, EventEnvelope, DoctorReportDto, OrchestrationSnapshotDto } from '../api'

const { t } = useI18n()

const activeTab = ref<'approval' | 'tasks' | 'diff' | 'events' | 'doctor'>('approval')
const collapsed = defineModel<boolean>('collapsed', { default: false })

defineProps<{
  approvals: ApprovalDto[]
  events: EventEnvelope[]
  doctor: DoctorReportDto | null
  orchestration: OrchestrationSnapshotDto | null
  shellCommand: string
  busy: boolean
  selectedSessionId: string | null
  currentProjectPath: string | undefined
}>()

const emit = defineEmits<{
  'request-approval': []
  'decide-approval': [approval: ApprovalDto, decision: 'approved' | 'rejected']
  'update:shellCommand': [value: string]
}>()

const tabs = [
  { key: 'approval' as const, label: () => t('context.approval') },
  { key: 'tasks' as const, label: () => 'Tasks' },
  { key: 'diff' as const, label: () => t('context.diff') },
  { key: 'events' as const, label: () => t('context.events') },
  { key: 'doctor' as const, label: () => t('context.doctor') },
]
</script>

<template>
  <aside class="right-rail" :class="{ collapsed }">
    <button
      class="right-rail-toggle"
      :title="collapsed ? t('app.expand') : t('app.collapse')"
      @click="collapsed = !collapsed"
    >
      <ChevronLeft v-if="!collapsed" :size="14" />
      <ChevronRight v-else :size="14" />
    </button>

    <template v-if="!collapsed">
      <div class="context-tabs">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          class="context-tab"
          :class="{ active: activeTab === tab.key }"
          @click="activeTab = tab.key"
        >
          {{ tab.label() }}
        </button>
      </div>

      <ApprovalTab
        v-if="activeTab === 'approval'"
        :approvals="approvals"
        :shell-command="shellCommand"
        :busy="busy"
        :selected-session-id="selectedSessionId"
        @request-approval="emit('request-approval')"
        @decide-approval="(a, d) => emit('decide-approval', a, d)"
        @update:shell-command="emit('update:shellCommand', $event)"
      />
      <OrchestrationTab v-if="activeTab === 'tasks'" :snapshot="orchestration" />
      <DiffTab v-if="activeTab === 'diff'" />
      <EventsTab v-if="activeTab === 'events'" :events="events" />
      <DoctorTab v-if="activeTab === 'doctor'" :doctor="doctor" />
    </template>
  </aside>
</template>
