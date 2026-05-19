<script setup lang="ts">
import type { EventEnvelope } from '../api'

defineProps<{
  events: EventEnvelope[]
}>()
</script>

<template>
  <section class="panel">
    <ol class="event-list">
      <li v-for="event in events" :key="event.seq">
        <span>{{ event.type }}</span>
        <small>#{{ event.seq }}</small>
      </li>
    </ol>
  </section>
</template>
