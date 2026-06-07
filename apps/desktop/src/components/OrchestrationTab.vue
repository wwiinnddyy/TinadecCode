<script setup lang="ts">
import { computed } from 'vue'
import { AlertTriangle, Archive, CheckCircle2, Layers3, Wrench } from '@lucide/vue'
import type { OrchestrationSnapshotDto, ToolExecutionTimelineItemDto } from '../api'

const props = defineProps<{
  snapshot: OrchestrationSnapshotDto | null
  toolExecutions: ToolExecutionTimelineItemDto[]
}>()

const hasSnapshot = computed(() => Boolean(props.snapshot?.run))
</script>

<template>
  <section class="orchestration-tab">
    <div v-if="!hasSnapshot" class="orchestration-empty">
      No orchestration run yet.
    </div>

    <template v-else-if="snapshot">
      <article class="orchestration-block">
        <div class="orchestration-block-head">
          <Layers3 :size="15" />
          <strong>Run</strong>
        </div>
        <p>{{ snapshot.run?.summary }}</p>
        <div class="orchestration-tags">
          <span>{{ snapshot.run?.status }}</span>
          <span>{{ snapshot.nodes.length }} nodes</span>
          <span>{{ snapshot.assignments.length }} assignments</span>
        </div>
      </article>

      <article class="orchestration-block">
        <div class="orchestration-block-head">
          <AlertTriangle :size="15" />
          <strong>Supervision</strong>
        </div>
        <div v-if="snapshot.supervision_findings.length === 0" class="quiet">
          No findings.
        </div>
        <div v-for="finding in snapshot.supervision_findings" :key="finding.id" class="orchestration-finding">
          <span>{{ finding.severity }} · {{ finding.category }}</span>
          <p>{{ finding.summary }}</p>
          <small>{{ finding.recommendation }}</small>
        </div>
      </article>

      <article class="orchestration-block">
        <div class="orchestration-block-head">
          <Wrench :size="15" />
          <strong>Tool Executions</strong>
        </div>
        <div v-if="toolExecutions.length === 0" class="quiet">
          No tool executions.
        </div>
        <div
          v-for="execution in toolExecutions"
          :key="execution.id"
          class="tool-execution-row"
          :class="{ risky: execution.requires_approval }"
        >
          <div class="tool-execution-top">
            <span>{{ execution.status }}</span>
            <strong>{{ execution.tool_display_name }}</strong>
          </div>
          <p>{{ execution.summary }}</p>
          <div class="tool-execution-meta">
            <span>{{ execution.source }}</span>
            <span>{{ execution.risk }}</span>
            <span v-if="execution.approval_id">approval {{ execution.approval_id }}</span>
            <span v-if="execution.step_result_id">step {{ execution.step_result_id }}</span>
          </div>
          <div v-if="execution.evidence.length > 0" class="tool-execution-evidence">
            <small v-for="item in execution.evidence" :key="item">{{ item }}</small>
          </div>
          <div class="tool-execution-events">
            <span v-for="eventType in execution.event_types" :key="eventType">{{ eventType }}</span>
          </div>
        </div>
      </article>

      <article class="orchestration-block">
        <div class="orchestration-block-head">
          <Archive :size="15" />
          <strong>Context Packs</strong>
        </div>
        <div v-if="snapshot.context_packs.length === 0" class="quiet">
          No context packs.
        </div>
        <div v-for="pack in snapshot.context_packs" :key="pack.id" class="context-pack-row">
          <p>{{ pack.summary }}</p>
          <div class="orchestration-tags">
            <span>{{ pack.token_budget }} tokens</span>
            <span>{{ Math.round(pack.compression_ratio * 100) }}%</span>
          </div>
        </div>
      </article>

      <article class="orchestration-block">
        <div class="orchestration-block-head">
          <CheckCircle2 :size="15" />
          <strong>Step Results</strong>
        </div>
        <div v-if="snapshot.step_results.length === 0" class="quiet">
          No step results.
        </div>
        <div v-for="result in snapshot.step_results" :key="result.id" class="step-result-row">
          <span>{{ result.status }}</span>
          <p>{{ result.summary }}</p>
        </div>
      </article>
    </template>
  </section>
</template>
