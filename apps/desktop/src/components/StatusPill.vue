<script setup lang="ts">
import { toneForStatus } from '../format'

defineProps<{
  status: string
}>()
</script>

<template>
  <span class="status-pill" :class="toneForStatus(status)">{{ status }}</span>
</template>
