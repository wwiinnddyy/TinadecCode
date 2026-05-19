<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { api, type ApprovalDto, type DoctorReportDto, type EventEnvelope, type MessageDto, type ModelSettingsDto, type OrchestrationSnapshotDto, type ProjectDto, type SessionDto } from '../api'
import { basenameFromPath } from '../format'
import AppHeader from '../components/AppHeader.vue'
import AppSidebar from '../components/AppSidebar.vue'
import ChatPanel from '../components/ChatPanel.vue'
import ContextPanel from '../components/ContextPanel.vue'

const router = useRouter()
const { t } = useI18n()

const projects = ref<ProjectDto[]>([])
const sessions = ref<SessionDto[]>([])
const messages = ref<MessageDto[]>([])
const approvals = ref<ApprovalDto[]>([])
const events = ref<EventEnvelope[]>([])
const doctor = ref<DoctorReportDto | null>(null)
const modelSettings = ref<ModelSettingsDto | null>(null)
const orchestration = ref<OrchestrationSnapshotDto | null>(null)

const selectedProjectId = ref<string | null>(null)
const selectedSessionId = ref<string | null>(null)
const pendingSessionId = ref<string | null>(null)
const draft = ref('')
const modelBaseUrl = ref('https://api.openai.com/v1')
const modelName = ref('gpt-5.4-mini')
const modelApiKey = ref('')
const shellCommand = ref('npm test')
const busy = ref(false)
const error = ref<string | null>(null)
const eventSource = ref<EventSource | null>(null)
const rightRailCollapsed = ref(false)

const currentProject = computed(() => projects.value.find((project) => project.id === selectedProjectId.value) ?? null)
const currentSession = computed(() => sessions.value.find((session) => session.id === selectedSessionId.value) ?? null)
const recentEvents = computed(() => events.value.slice(-8).reverse())

function generateTitle(content: string): string {
  const trimmed = content.trim()
  if (!trimmed) return t('chat.newChat')
  const firstLine = trimmed.split('\n')[0]
  if (firstLine.length <= 50) return firstLine
  return firstLine.substring(0, 47) + '...'
}

async function run(label: string, action: () => Promise<void>) {
  busy.value = true
  error.value = null
  try {
    await action()
  } catch (err) {
    error.value = err instanceof Error ? err.message : `${label} failed`
  } finally {
    busy.value = false
  }
}

async function loadInitial() {
  await run('load', async () => {
    const [projectList, settings, report] = await Promise.all([
      api.listProjects(),
      api.getModelSettings(),
      api.doctor(),
    ])
    projects.value = projectList
    modelSettings.value = settings
    doctor.value = report
    modelBaseUrl.value = settings.base_url
    modelName.value = settings.model
    selectedProjectId.value = projectList[0]?.id ?? null
    await loadSessions()
  })
}

async function loadSessions() {
  if (projects.value.length === 0) {
    sessions.value = []
    selectedSessionId.value = null
    return
  }

  const allSessions = await Promise.all(
    projects.value.map((p) => api.listSessions(p.id))
  )
  sessions.value = allSessions.flat()

  if (!selectedProjectId.value) {
    selectedSessionId.value = null
    return
  }

  const projectSessions = sessions.value.filter((s) => s.project_id === selectedProjectId.value)
  if (!projectSessions.find((s) => s.id === selectedSessionId.value)) {
    selectedSessionId.value = projectSessions[0]?.id ?? null
  }
}

async function loadMessagesAndApprovals() {
  if (!selectedSessionId.value) {
    messages.value = []
    approvals.value = []
    orchestration.value = null
    return
  }

  const [messageList, approvalList, orchestrationSnapshot] = await Promise.all([
    api.listMessages(selectedSessionId.value),
    api.listApprovals(selectedSessionId.value),
    api.getOrchestrationSnapshot(selectedSessionId.value),
  ])
  messages.value = messageList
  approvals.value = approvalList
  orchestration.value = orchestrationSnapshot
}

async function openProject() {
  await run('open project', async () => {
    const path = await window.tinadec.openProjectDialog()
    if (!path) return

    const project = await api.createProject(basenameFromPath(path), path)
    projects.value = [project, ...projects.value.filter((item) => item.id !== project.id)]
    selectedProjectId.value = project.id
  })
}

async function createSession(projectId: string) {
  if (pendingSessionId.value) {
    const existing = sessions.value.find((s) => s.id === pendingSessionId.value)
    if (existing && existing.project_id === projectId) {
      selectedSessionId.value = pendingSessionId.value
      selectedProjectId.value = projectId
      return
    }
  }

  await run('create session', async () => {
    const session = await api.createSession(projectId, 'Tinadec session')
    sessions.value = [session, ...sessions.value]
    selectedSessionId.value = session.id
    selectedProjectId.value = projectId
    pendingSessionId.value = session.id
  })
}

async function sendMessage() {
  const content = draft.value.trim()
  if (!content) return
  await handleSend(content)
}

async function handleWelcomeSend(content: string) {
  await handleSend(content)
}

async function handleSend(content: string) {
  await run('send message', async () => {
    let sessionId = selectedSessionId.value

    if (!sessionId && selectedProjectId.value) {
      const session = await api.createSession(selectedProjectId.value, 'Tinadec session')
      sessions.value = [session, ...sessions.value]
      selectedSessionId.value = session.id
      sessionId = session.id
      pendingSessionId.value = session.id
    }

    if (!sessionId) {
      throw new Error('Open a project before sending a message.')
    }

    draft.value = ''
    await api.postMessage(sessionId, content)

    if (pendingSessionId.value === sessionId) {
      const title = generateTitle(content)
      try {
        await api.updateSessionTitle(sessionId, title)
        const idx = sessions.value.findIndex((s) => s.id === sessionId)
        if (idx !== -1) {
          sessions.value[idx] = { ...sessions.value[idx], title }
        }
      } catch {
        const idx = sessions.value.findIndex((s) => s.id === sessionId)
        if (idx !== -1) {
          sessions.value[idx] = { ...sessions.value[idx], title }
        }
      }
      pendingSessionId.value = null
    }

    await loadMessagesAndApprovals()
  })
}

async function requestShellApproval() {
  await run('request approval', async () => {
    const approval = await api.createShellApproval(selectedSessionId.value, shellCommand.value, currentProject.value?.path)
    approvals.value = [approval, ...approvals.value]
  })
}

async function decideApproval(approval: ApprovalDto, decision: 'approved' | 'rejected') {
  await run('decide approval', async () => {
    await api.decideApproval(approval.id, decision)
    await loadMessagesAndApprovals()
  })
}

function reconnectEvents() {
  eventSource.value?.close()
  eventSource.value = api.connectEvents(selectedSessionId.value, async (event) => {
    const bySeq = new Map(events.value.map((item) => [item.seq, item]))
    bySeq.set(event.seq, event)
    events.value = [...bySeq.values()].sort((left, right) => left.seq - right.seq).slice(-80)
    if (
      event.type.startsWith('message.') ||
      event.type.startsWith('approval.') ||
      event.type.startsWith('tool.') ||
      event.type.startsWith('run.') ||
      event.type.startsWith('task') ||
      event.type.startsWith('supervision.') ||
      event.type.startsWith('context.') ||
      event.type.startsWith('step.')
    ) {
      await loadMessagesAndApprovals()
    }
  })
}

watch(selectedProjectId, () => {
  void loadSessions()
})

watch(selectedSessionId, () => {
  void loadMessagesAndApprovals()
  reconnectEvents()
})

onMounted(() => {
  void loadInitial()
  reconnectEvents()
})

onUnmounted(() => {
  eventSource.value?.close()
})
</script>

<template>
  <main class="shell">
    <AppHeader :busy="busy" />

    <section v-if="error" class="error-strip">{{ error }}</section>

    <section class="workspace">
      <AppSidebar
        :projects="projects"
        :sessions="sessions"
        :selected-project-id="selectedProjectId"
        :selected-session-id="selectedSessionId"
        :busy="busy"
        @select-project="selectedProjectId = $event"
        @select-session="selectedSessionId = $event"
        @create-session="createSession"
        @open-project="openProject"
        @go-market="router.push('/market')"
        @go-settings="router.push('/settings')"
      />

      <ChatPanel
        :messages="messages"
        :sessions="sessions"
        :projects="projects"
        :current-session="currentSession"
        :current-project="currentProject"
        :selected-project-id="selectedProjectId"
        :model-name="modelName"
        :orchestration="orchestration"
        :busy="busy"
        :draft="draft"
        @update:draft="draft = $event"
        @send="sendMessage"
        @welcome-send="handleWelcomeSend"
        @create-project="openProject"
        @select-project="selectedProjectId = $event"
      />

      <ContextPanel
        v-model:collapsed="rightRailCollapsed"
        :approvals="approvals"
        :events="recentEvents"
        :doctor="doctor"
        :orchestration="orchestration"
        :shell-command="shellCommand"
        :busy="busy"
        :selected-session-id="selectedSessionId"
        :current-project-path="currentProject?.path"
        @request-approval="requestShellApproval"
        @decide-approval="decideApproval"
        @update:shell-command="shellCommand = $event"
      />
    </section>
  </main>
</template>
