<script setup lang="ts">
import {
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  FilePlus,
  FileText,
  FileX,
  FileCog,
  GitCommitHorizontal,
  Loader2,
  Plus,
  Sparkles,
  ShieldCheck,
  ShieldX,
  Upload,
  RefreshCw,
} from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ApprovalDto } from '../../api'
import {
  type GitStatusFile,
  statusToLabel,
  statusColor,
} from '../../composables/useGitOperation'
import { useAiCommitMessage } from '../../composables/useAiCommitMessage'
import CommitMessageEditor from './CommitMessageEditor.vue'
import DiffViewer from './DiffViewer.vue'
import { reconstructFromHunks, type DiffFileEntry } from './diffUtils'
import { parseUnifiedDiff } from '../../gitDiffParser'

interface Props {
  cwd: string | undefined
  sessionId: string | null
  // Git operation state (injected from parent composable)
  loading: boolean
  operationLoading: boolean
  error: string | null
  feedback: string | null
  statusFiles: GitStatusFile[]
  commitMessage: string
  selectedPaths: Set<string>
  selectAll: boolean
  selectAllIndeterminate: boolean
  // Diff sections
  diffText: string
  diffFiles: Array<{
    path: string
    previous_path?: string | null
    change_type: string
    additions: number
    deletions: number
    binary: boolean
    truncated: boolean
  }>
  // Push state
  pushReady: boolean
  pushBlockers: string[]
  hasPushCandidate: boolean
  canRequestPushApproval: boolean
  canRequestPullApproval: boolean
  behind: number
  // Approvals
  indexApproval: ApprovalDto | null
  commitApproval: ApprovalDto | null
  pushApproval: ApprovalDto | null
  canRequestIndexApproval: boolean
  canRequestCommitApproval: boolean
  canDecideIndexApproval: boolean
  canDecideCommitApproval: boolean
  canDecidePushApproval: boolean
  recentCommits: string[]
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:commitMessage': [value: string]
  'refresh': []
  'toggle-path': [path: string]
  'toggle-select-all': []
  'request-stage': []
  'request-unstage': []
  'execute-index': []
  'request-commit': []
  'execute-commit': []
  'request-push': []
  'execute-push': []
  'request-pull': []
  'decide-approval': [approval: ApprovalDto, decision: 'approved' | 'rejected']
}>()

const { t } = useI18n()

// ---- Diff preview ----
const showDiffPreview = ref(false)
const selectedDiffFile = ref<string | null>(null)

const parsedDiff = computed(() => parseUnifiedDiff(props.diffText))
const diffEntries = computed<DiffFileEntry[]>(() => {
  return parsedDiff.value.files.map((file) => {
    const meta = props.diffFiles.find((item) => item.path === file.path)
    const { original, modified } = reconstructFromHunks(file)
    return {
      path: file.path,
      previousPath: file.previous_path,
      originalContent: original,
      modifiedContent: modified,
      additions: meta?.additions,
      deletions: meta?.deletions,
      binary: file.binary,
      truncated: meta?.truncated,
      changeType: meta?.change_type ?? file.change_type,
    }
  })
})

watch(diffEntries, (entries) => {
  if (!entries.some((e) => e.path === selectedDiffFile.value)) {
    selectedDiffFile.value = entries[0]?.path ?? null
  }
}, { immediate: true })

// ---- AI commit message ----
const sessionIdRef = computed(() => props.sessionId)
const {
  generating: aiGenerating,
  aiError: aiErr,
  aiSuggestion,
  canGenerate: canAiGenerate,
  generate: aiGenerate,
  generateLocalSuggestion,
} = useAiCommitMessage(sessionIdRef)

const showAiPanel = ref(false)

async function handleAiGenerate() {
  showAiPanel.value = true
  // Generate local suggestion first for immediate feedback
  const local = generateLocalSuggestion(props.statusFiles, [])
  if (local) {
    emit('update:commitMessage', local.fullMessage)
  }
  // Then try AI generation
  await aiGenerate(props.statusFiles, [], props.statusFiles[0]?.path)
  if (aiSuggestion.value) {
    emit('update:commitMessage', aiSuggestion.value.fullMessage)
  }
}

// ---- Commit convention check ----
const conventionCheck = computed(() => {
  const msg = props.commitMessage.trim()
  if (!msg) return null
  const lines = msg.split(/\r?\n/)
  const header = lines[0] ?? ''
  const match = /^(\w+)(?:\(([^)]*)\))?!?\s*:\s*(.+)$/.exec(header)
  if (!match) {
    return {
      valid: false,
      message: t('context.gitConvInvalidHeader'),
    }
  }
  const subject = match[3] ?? ''
  if (subject.length > 72) {
    return {
      valid: false,
      message: t('context.gitConvSubjectTooLong'),
    }
  }
  if (subject.length > 50) {
    return {
      valid: true,
      warning: t('context.gitConvSubjectWarn'),
    }
  }
  const validTypes = ['feat', 'fix', 'docs', 'style', 'refactor', 'perf', 'test', 'build', 'ci', 'chore', 'revert']
  if (!validTypes.includes(match[1] ?? '')) {
    return {
      valid: false,
      message: t('context.gitConvInvalidType'),
    }
  }
  // Check body line length
  const bodyLines = lines.slice(1).filter((l) => l.trim())
  const longBodyLines = bodyLines.filter((l) => l.length > 72)
  if (longBodyLines.length > 0) {
    return {
      valid: true,
      warning: t('context.gitConvBodyTooLong'),
    }
  }
  return { valid: true }
})

// ---- File status icon helper ----
function statusIcon(status?: string) {
  const label = statusToLabel(status)
  switch (label) {
    case 'A': return FilePlus
    case 'D': return FileX
    case 'R':
    case 'C': return FileCog
    case 'M': return FileText
    default: return FileText
  }
}

function statusColorClass(status?: string) {
  return `status-${statusColor(status)}`
}

// ---- Collapsible sections ----
const filesExpanded = ref(true)
const commitExpanded = ref(true)
const pushExpanded = ref(true)
</script>

<template>
  <div class="git-changes-view">
    <!-- Error banner -->
    <div v-if="error" class="git-changes-error">
      <AlertTriangle :size="14" />
      <span>{{ error }}</span>
    </div>

    <!-- File changes section -->
    <div class="git-section">
      <button class="git-section-header" @click="filesExpanded = !filesExpanded">
        <component :is="filesExpanded ? ChevronDown : ChevronRight" :size="14" />
        <GitCommitHorizontal :size="14" />
        <span>{{ t('context.gitChanges') }}</span>
        <span class="git-section-count">{{ statusFiles.length }}</span>
        <div class="git-section-actions" @click.stop>
          <label class="git-select-all-cb">
            <input
              type="checkbox"
              :checked="selectAll"
              :indeterminate="selectAllIndeterminate"
              @change="emit('toggle-select-all')"
            />
          </label>
          <button
            class="icon-button git-section-refresh"
            :title="t('context.refreshGitPlan')"
            :disabled="loading"
            @click="emit('refresh')"
          >
            <RefreshCw :size="12" :class="{ spinning: loading }" />
          </button>
        </div>
      </button>

      <div v-show="filesExpanded" class="git-section-body">
        <div v-if="statusFiles.length === 0" class="git-empty-state">
          {{ t('context.gitNoChanges') }}
        </div>
        <div v-else class="git-file-list">
          <label
            v-for="file in statusFiles"
            :key="file.path"
            class="git-file-row"
            :class="statusColorClass(file.status ?? file.unstaged_status)"
          >
            <input
              type="checkbox"
              :checked="selectedPaths.has(file.path)"
              @change="emit('toggle-path', file.path)"
            />
            <component :is="statusIcon(file.status ?? file.unstaged_status)" :size="13" class="git-file-icon" />
            <span class="git-file-path" :title="file.path">{{ file.path }}</span>
            <span class="git-file-status-badge">{{ statusToLabel(file.status ?? file.unstaged_status) }}</span>
          </label>
        </div>

        <!-- Diff preview toggle -->
        <button
          v-if="diffEntries.length > 0"
          class="git-diff-toggle"
          @click="showDiffPreview = !showDiffPreview"
        >
          <component :is="showDiffPreview ? ChevronDown : ChevronRight" :size="12" />
          <span>{{ t('context.gitDiffPreview') }}</span>
          <small>{{ diffEntries.length }} {{ t('context.gitDiffFiles') }}</small>
        </button>
        <div v-if="showDiffPreview && diffEntries.length > 0" class="git-diff-preview">
          <DiffViewer
            :files="diffEntries"
            :selected-file-path="selectedDiffFile"
            :enable-hunk-actions="true"
            @update:selected-file-path="selectedDiffFile = $event"
            @stage-hunk="(payload) => emit('toggle-path', payload.filePath)"
          />
        </div>
      </div>
    </div>

    <!-- Stage/Unstage actions -->
    <div class="git-stage-actions">
      <button
        class="secondary-button git-action-btn"
        :disabled="operationLoading || !canRequestIndexApproval"
        @click="emit('request-stage')"
      >
        <Plus :size="13" />
        <span>{{ t('context.gitStage') }}</span>
      </button>
      <button
        class="secondary-button git-action-btn"
        :disabled="operationLoading || !canRequestIndexApproval"
        @click="emit('request-unstage')"
      >
        <span>{{ t('context.gitUnstage') }}</span>
      </button>
      <button
        v-if="indexApproval?.status === 'approved'"
        class="secondary-button git-action-btn git-execute-btn"
        :disabled="operationLoading"
        @click="emit('execute-index')"
      >
        <CheckCircle2 :size="13" />
        <span>{{ t('context.gitExecuteIndexUpdate') }}</span>
      </button>
      <div v-if="canDecideIndexApproval" class="git-approval-decide">
        <button class="icon-button approve" :title="t('approval.approve')" @click="emit('decide-approval', indexApproval!, 'approved')">
          <CheckCircle2 :size="14" />
        </button>
        <button class="icon-button reject" :title="t('approval.reject')" @click="emit('decide-approval', indexApproval!, 'rejected')">
          <ShieldX :size="14" />
        </button>
      </div>
    </div>

    <!-- Commit message section -->
    <div class="git-section">
      <button class="git-section-header" @click="commitExpanded = !commitExpanded">
        <component :is="commitExpanded ? ChevronDown : ChevronRight" :size="14" />
        <GitCommitHorizontal :size="14" />
        <span>{{ t('context.gitCommitMessage') }}</span>
      </button>

      <div v-show="commitExpanded" class="git-section-body">
        <!-- AI generation panel -->
        <div class="git-ai-bar">
          <button
            class="secondary-button git-ai-btn"
            :disabled="!canAiGenerate || statusFiles.length === 0"
            @click="handleAiGenerate"
          >
            <component :is="aiGenerating ? Loader2 : Sparkles" :size="13" :class="{ spinning: aiGenerating }" />
            <span>{{ aiGenerating ? t('context.gitAiGenerating') : t('context.gitAiGenerate') }}</span>
          </button>
          <div v-if="aiErr" class="git-ai-error">
            <AlertTriangle :size="12" />
            <span>{{ aiErr }}</span>
          </div>
        </div>

        <CommitMessageEditor
          :model-value="commitMessage"
          :recent-commits="recentCommits"
          @update:model-value="emit('update:commitMessage', $event)"
        />

        <!-- Convention check -->
        <div v-if="conventionCheck" class="git-convention-check" :class="{ valid: conventionCheck.valid, invalid: !conventionCheck.valid }">
          <component :is="conventionCheck.valid ? CheckCircle2 : AlertTriangle" :size="13" />
          <span>{{ conventionCheck.message ?? conventionCheck.warning }}</span>
        </div>

        <!-- Commit actions -->
        <div class="git-commit-actions">
          <button
            class="secondary-button git-action-btn"
            :disabled="operationLoading || !canRequestCommitApproval"
            @click="emit('request-commit')"
          >
            <ShieldCheck :size="13" />
            <span>{{ t('context.gitRequestCommitApproval') }}</span>
          </button>
          <button
            class="secondary-button git-action-btn git-execute-btn"
            :disabled="operationLoading || commitApproval?.status !== 'approved'"
            @click="emit('execute-commit')"
          >
            <CheckCircle2 :size="13" />
            <span>{{ t('context.gitExecuteCommit') }}</span>
          </button>
        </div>
        <div v-if="canDecideCommitApproval" class="git-approval-decide">
          <button class="icon-button approve" :title="t('approval.approve')" @click="emit('decide-approval', commitApproval!, 'approved')">
            <CheckCircle2 :size="14" />
          </button>
          <button class="icon-button reject" :title="t('approval.reject')" @click="emit('decide-approval', commitApproval!, 'rejected')">
            <ShieldX :size="14" />
          </button>
        </div>
      </div>
    </div>

    <!-- Push/Pull section -->
    <div class="git-section">
      <button class="git-section-header" @click="pushExpanded = !pushExpanded">
        <component :is="pushExpanded ? ChevronDown : ChevronRight" :size="14" />
        <Upload :size="14" />
        <span>{{ t('context.gitSync') }}</span>
        <div class="git-section-actions" @click.stop>
          <span v-if="behind > 0" class="git-behind-badge" :title="t('context.gitBehind')">
            ↓{{ behind }}
          </span>
        </div>
      </button>

      <div v-show="pushExpanded" class="git-section-body">
        <!-- Push readiness -->
        <div class="git-push-status" :class="{ ready: pushReady, blocked: !pushReady }">
          <component :is="pushReady ? CheckCircle2 : AlertTriangle" :size="16" />
          <div>
            <strong>{{ pushReady ? t('context.gitPushReady') : t('context.gitPushBlocked') }}</strong>
          </div>
        </div>

        <!-- Blockers -->
        <div v-if="pushBlockers.length > 0" class="git-blockers">
          <small v-for="blocker in pushBlockers" :key="blocker">{{ blocker }}</small>
        </div>

        <!-- Sync actions -->
        <div class="git-sync-actions">
          <button
            class="secondary-button git-action-btn"
            :disabled="operationLoading || !canRequestPushApproval"
            @click="emit('request-push')"
          >
            <Upload :size="13" />
            <span>{{ t('context.gitRequestPushApproval') }}</span>
          </button>
          <button
            v-if="pushApproval?.status === 'approved'"
            class="secondary-button git-action-btn git-execute-btn"
            :disabled="operationLoading"
            @click="emit('execute-push')"
          >
            <Upload :size="13" />
            <span>{{ t('context.gitExecutePush') }}</span>
          </button>
          <button
            v-if="canRequestPullApproval"
            class="secondary-button git-action-btn"
            :disabled="operationLoading"
            @click="emit('request-pull')"
          >
            <RefreshCw :size="13" />
            <span>{{ t('context.gitPull') }}</span>
          </button>
        </div>
        <div v-if="canDecidePushApproval" class="git-approval-decide">
          <button class="icon-button approve" :title="t('approval.approve')" @click="emit('decide-approval', pushApproval!, 'approved')">
            <CheckCircle2 :size="14" />
          </button>
          <button class="icon-button reject" :title="t('approval.reject')" @click="emit('decide-approval', pushApproval!, 'rejected')">
            <ShieldX :size="14" />
          </button>
        </div>
      </div>
    </div>

    <!-- Feedback -->
    <div v-if="feedback" class="git-feedback">
      <ShieldCheck :size="13" />
      <span>{{ feedback }}</span>
    </div>
  </div>
</template>

<style scoped>
.git-changes-view {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.git-changes-error {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  color: var(--text-reject, #f85149);
  background: var(--bg-status-warn);
  border: 1px solid rgba(248, 81, 73, 0.25);
  border-radius: 6px;
  font-size: 12px;
}

.git-section {
  border: 1px solid var(--border-muted);
  border-radius: 8px;
  overflow: hidden;
}

.git-section-header {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  padding: 8px 10px;
  background: var(--bg-secondary);
  border: 0;
  color: var(--text-primary);
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  text-align: left;
  transition: background 0.12s;
}

.git-section-header:hover {
  background: var(--bg-hover);
}

.git-section-count {
  margin-left: auto;
  padding: 1px 6px;
  background: var(--bg-tertiary);
  border-radius: 999px;
  font-size: 10px;
  font-weight: 700;
  color: var(--text-secondary);
}

.git-section-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-left: 8px;
}

.git-select-all-cb {
  display: flex;
  align-items: center;
  cursor: pointer;
}

.git-section-refresh {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.git-section-body {
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.git-empty-state {
  padding: 16px 10px;
  text-align: center;
  color: var(--text-muted);
  font-size: 12px;
}

.git-file-list {
  display: flex;
  flex-direction: column;
  gap: 1px;
  max-height: 280px;
  overflow-y: auto;
}

.git-file-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 8px;
  cursor: pointer;
  border-radius: 4px;
  transition: background 0.1s;
}

.git-file-row:hover {
  background: var(--bg-hover);
}

.git-file-icon {
  flex-shrink: 0;
}

.git-file-path {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-primary);
  font-family: 'Geist Mono', ui-monospace, monospace;
  font-size: 11.5px;
}

.git-file-status-badge {
  flex-shrink: 0;
  min-width: 16px;
  text-align: center;
  font-size: 10px;
  font-weight: 700;
  font-family: 'Geist Mono', ui-monospace, monospace;
  padding: 1px 4px;
  border-radius: 3px;
}

/* Status color variants */
.status-added .git-file-icon { color: #3fb950; }
.status-added .git-file-status-badge { color: #3fb950; background: rgba(63, 185, 80, 0.12); }

.status-modified .git-file-icon { color: #d29922; }
.status-modified .git-file-status-badge { color: #d29922; background: rgba(210, 153, 34, 0.12); }

.status-deleted .git-file-icon { color: #f85149; }
.status-deleted .git-file-status-badge { color: #f85149; background: rgba(248, 81, 73, 0.12); }

.status-renamed .git-file-icon { color: #58a6ff; }
.status-renamed .git-file-status-badge { color: #58a6ff; background: rgba(88, 166, 255, 0.12); }

.status-untracked .git-file-icon { color: #7d8590; }
.status-untracked .git-file-status-badge { color: #7d8590; background: rgba(125, 133, 144, 0.12); }

.status-conflict .git-file-icon { color: #f85149; }
.status-conflict .git-file-status-badge { color: #f85149; background: rgba(248, 81, 73, 0.2); }

.git-diff-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  background: transparent;
  border: 1px solid var(--border-muted);
  border-radius: 6px;
  color: var(--text-secondary);
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.1s;
}

.git-diff-toggle:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.git-diff-toggle small {
  margin-left: auto;
  color: var(--text-muted);
  font-size: 10px;
}

.git-diff-preview {
  min-height: 280px;
  height: 400px;
  border-radius: 8px;
  overflow: hidden;
}

.git-stage-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
}

.git-action-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  padding: 6px 10px;
  font-size: 12px;
  white-space: nowrap;
}

.git-execute-btn {
  background: var(--bg-primary-button);
  color: #fff;
  border-color: var(--bg-primary-button);
}

.git-execute-btn:hover:not(:disabled) {
  background: var(--bg-primary-button-hover);
}

.git-approval-decide {
  display: flex;
  gap: 4px;
}

.git-ai-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.git-ai-btn {
  background: linear-gradient(135deg, rgba(88, 166, 255, 0.12), rgba(163, 113, 247, 0.12));
  border-color: rgba(88, 166, 255, 0.3);
  color: var(--accent-primary);
}

.git-ai-btn:hover:not(:disabled) {
  background: linear-gradient(135deg, rgba(88, 166, 255, 0.2), rgba(163, 113, 247, 0.2));
}

.git-ai-error {
  display: flex;
  align-items: center;
  gap: 4px;
  color: var(--text-reject, #f85149);
  font-size: 11px;
}

.git-convention-check {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  border-radius: 6px;
  font-size: 11px;
}

.git-convention-check.valid {
  color: #3fb950;
  background: rgba(63, 185, 80, 0.08);
}

.git-convention-check.invalid {
  color: var(--text-reject, #f85149);
  background: rgba(248, 81, 73, 0.08);
}

.git-commit-actions {
  display: flex;
  gap: 6px;
}

.git-push-status {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border-radius: 6px;
}

.git-push-status.ready {
  background: rgba(63, 185, 80, 0.08);
  color: #3fb950;
}

.git-push-status.blocked {
  background: rgba(210, 153, 34, 0.08);
  color: #d29922;
}

.git-push-status strong {
  font-size: 12px;
  color: var(--text-primary);
}

.git-blockers {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.git-blockers small {
  padding: 2px 6px;
  background: var(--bg-tertiary);
  border-radius: 4px;
  font-size: 10px;
  color: var(--text-muted);
}

.git-sync-actions {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.git-behind-badge {
  padding: 1px 6px;
  background: rgba(210, 153, 34, 0.15);
  border-radius: 999px;
  font-size: 10px;
  font-weight: 700;
  color: #d29922;
}

.git-feedback {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  background: var(--bg-selected);
  border: 1px solid var(--bg-selected-outline);
  border-radius: 6px;
  font-size: 12px;
  color: var(--text-primary);
}

.spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
