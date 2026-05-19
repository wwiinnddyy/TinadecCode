<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { SessionDto, ProjectDto } from '../api'
import { toneForStatus } from '../format'
import StatusPill from './StatusPill.vue'

const { t } = useI18n()

defineProps<{
  currentSession: SessionDto | null
  currentProject: ProjectDto | null
}>()
</script>

<template>
  <div class="conversation-head">
    <div>
      <h1>{{ currentSession?.title ?? t('chat.title') }}</h1>
      <p>{{ currentProject?.path ?? t('chat.noProject') }}</p>
    </div>
    <StatusPill :status="currentSession?.status ?? 'neutral'" />
  </div>
</template>
