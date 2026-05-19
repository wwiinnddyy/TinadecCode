<script setup lang="ts">
import { Bot, User } from '@lucide/vue'
import type { MessageDto } from '../api'

defineProps<{
  message: MessageDto
}>()
</script>

<template>
  <article class="message" :class="message.role">
    <div class="avatar">
      <User v-if="message.role === 'user'" :size="14" />
      <Bot v-else :size="14" />
    </div>
    <p>{{ message.content }}</p>
  </article>
</template>
