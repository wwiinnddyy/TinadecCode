<script setup lang="ts">
import { computed } from 'vue'
import { GitBranch, ShieldCheck } from '@lucide/vue'
import type { AgentAssignmentDto, OrchestrationSnapshotDto, TaskNodeDto } from '../api'

const props = defineProps<{
  snapshot: OrchestrationSnapshotDto | null
}>()

const assignmentsByNode = computed(() => {
  const map = new Map<string, AgentAssignmentDto[]>()
  for (const assignment of props.snapshot?.assignments ?? []) {
    const list = map.get(assignment.task_node_id) ?? []
    list.push(assignment)
    map.set(assignment.task_node_id, list)
  }
  return map
})

function nodeAssignments(node: TaskNodeDto) {
  return assignmentsByNode.value.get(node.id) ?? []
}
</script>

<template>
  <section class="task-graph-panel">
    <div class="task-graph-head">
      <div>
        <span>Task Graph</span>
        <strong>{{ snapshot?.graph?.title ?? 'No graph yet' }}</strong>
      </div>
      <div v-if="snapshot?.run" class="task-graph-run">
        <GitBranch :size="15" />
        <span>{{ snapshot.run.status }}</span>
      </div>
    </div>

    <div v-if="!snapshot?.graph" class="task-graph-empty">
      Send a task to create the first two-layer orchestration graph.
    </div>

    <div v-else class="task-node-lane">
      <article v-for="node in snapshot.nodes" :key="node.id" class="task-node-card">
        <div class="task-node-top">
          <span class="task-node-index">{{ node.priority }}</span>
          <div>
            <strong>{{ node.title }}</strong>
            <p>{{ node.description }}</p>
          </div>
        </div>

        <div class="task-node-meta">
          <span>{{ node.status }}</span>
          <span>{{ node.risk }}</span>
        </div>

        <div class="task-node-agents">
          <template v-if="nodeAssignments(node).length > 0">
            <span v-for="assignment in nodeAssignments(node)" :key="assignment.id">
              {{ assignment.agent_name }} · {{ assignment.permission_mode }}
            </span>
          </template>
          <span v-else class="task-node-unassigned">No enabled executor assigned</span>
        </div>

        <div v-if="node.success_criteria.length > 0" class="task-node-criteria">
          <ShieldCheck :size="14" />
          <span>{{ node.success_criteria.join(' / ') }}</span>
        </div>
      </article>
    </div>
  </section>
</template>
