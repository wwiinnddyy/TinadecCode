<script setup lang="ts">
import { Check, ShieldX, Terminal } from '@lucide/vue'
import { useI18n } from 'vue-i18n'
import type { ApprovalDto } from '../api'

const { t } = useI18n()

defineProps<{
  approvals: ApprovalDto[]
  shellCommand: string
  busy: boolean
  selectedSessionId: string | null
}>()

const emit = defineEmits<{
  'request-approval': []
  'decide-approval': [approval: ApprovalDto, decision: 'approved' | 'rejected']
  'update:shellCommand': [value: string]
}>()

const pendingApprovals = (approvals: ApprovalDto[]) =>
  approvals.filter((a) => a.status === 'pending')
</script>

<template>
  <section class="panel">
    <div class="approval-input">
      <input
        :value="shellCommand"
        @input="emit('update:shellCommand', ($event.target as HTMLInputElement).value)"
      />
      <button
        class="icon-button"
        :title="t('approval.request')"
        :disabled="busy || !selectedSessionId"
        @click="emit('request-approval')"
      >
        <Terminal :size="14" />
      </button>
    </div>
    <article v-for="approval in pendingApprovals(approvals)" :key="approval.id" class="approval-row">
      <div>
        <strong>{{ approval.kind }}</strong>
        <p>{{ approval.summary }}</p>
      </div>
      <div class="approval-actions">
        <button class="icon-button approve" :title="t('approval.approve')" @click="emit('decide-approval', approval, 'approved')">
          <Check :size="14" />
        </button>
        <button class="icon-button reject" :title="t('approval.reject')" @click="emit('decide-approval', approval, 'rejected')">
          <ShieldX :size="14" />
        </button>
      </div>
    </article>
    <span v-if="pendingApprovals(approvals).length === 0" class="quiet">{{ t('context.noApprovals') }}</span>
  </section>
</template>
