<script setup lang="ts">
import { Bot } from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import type { MessageDto } from '../api'
import MessageItem from './MessageItem.vue'

const { t } = useI18n()

defineProps<{
  messages: MessageDto[]
}>()
</script>

<template>
  <div class="message-stream">
    <MessageItem v-for="message in messages" :key="message.id" :message="message" />
    <div v-if="messages.length === 0" class="empty-state">
      <Bot :size="24" />
      <span>{{ t('chat.ready') }}</span>
    </div>
  </div>
</template>
