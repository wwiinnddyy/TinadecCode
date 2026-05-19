<script setup lang="ts">
import type { DoctorReportDto } from '../api'
import { toneForStatus } from '../format'
import StatusPill from './StatusPill.vue'

defineProps<{
  doctor: DoctorReportDto | null
}>()
</script>

<template>
  <section class="panel">
    <div v-for="check in doctor?.checks ?? []" :key="check.name" class="doctor-row">
      <StatusPill :status="check.status" />
      <strong>{{ check.name }}</strong>
    </div>
  </section>
</template>
