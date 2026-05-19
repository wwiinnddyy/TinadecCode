<script setup lang="ts">
import ChatHeader from './ChatHeader.vue'
import MessageList from './MessageList.vue'
import ComposerBar from './ComposerBar.vue'
import WelcomeScreen from './WelcomeScreen.vue'
import TaskGraphPanel from './TaskGraphPanel.vue'
import type { MessageDto, SessionDto, ProjectDto, OrchestrationSnapshotDto } from '../api'

const props = defineProps<{
  messages: MessageDto[]
  sessions: SessionDto[]
  projects: ProjectDto[]
  currentSession: SessionDto | null
  currentProject: ProjectDto | null
  selectedProjectId: string | null
  modelName: string
  orchestration: OrchestrationSnapshotDto | null
  busy: boolean
  draft: string
}>()

const emit = defineEmits<{
  'update:draft': [value: string]
  'send': []
  'welcome-send': [content: string]
  'create-project': []
  'select-project': [id: string]
}>()
</script>

<template>
  <section class="conversation">
    <template v-if="messages.length === 0">
      <WelcomeScreen
        :projects="props.projects"
        :selected-project-id="selectedProjectId"
        :model-name="modelName"
        :busy="busy"
        @send="emit('welcome-send', $event)"
        @create-project="emit('create-project')"
        @select-project="emit('select-project', $event)"
      />
    </template>
    <template v-else>
      <ChatHeader :current-session="currentSession" :current-project="currentProject" />
      <TaskGraphPanel :snapshot="orchestration" />
      <MessageList :messages="messages" />
      <ComposerBar :busy="busy" :model-value="draft" @update:model-value="emit('update:draft', $event)" @submit="emit('send')" />
    </template>
  </section>
</template>
