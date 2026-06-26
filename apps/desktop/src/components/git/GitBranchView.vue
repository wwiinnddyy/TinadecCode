<script setup lang="ts">
import {
  AlertTriangle,
  CheckCircle2,
  GitBranch as GitBranchIcon,
  GitPullRequest,
  Loader2,
  Plus,
  RefreshCw,
  ShieldCheck,
  ShieldX,
  Star,
} from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { api, type ApprovalDto } from '../../api'
import { UiBadge, UiButton, UiInput, UiScrollArea } from '../ui'
import CommitCompare from './CommitCompare.vue'
import WorktreeManager from './WorktreeManager.vue'

const props = defineProps<{
  cwd?: string
  currentBranch: string
  checkoutApproval: ApprovalDto | null
  branchApproval: ApprovalDto | null
  canDecideCheckoutApproval: boolean
  canDecideBranchApproval: boolean
  operationLoading: boolean
}>()

const emit = defineEmits<{
  'checkout': [branch: string]
  'create-branch': [name: string]
  'decide-approval': [approval: ApprovalDto, decision: 'approved' | 'rejected']
  'execute-checkout': []
  'execute-create-branch': []
}>()

const { t } = useI18n()

// ---- State ----
const loading = ref(false)
const error = ref<string | null>(null)
const branches = ref<Array<{
  name: string
  is_current: boolean
  is_remote: boolean
  upstream?: string | null
  last_subject?: string
  last_date?: string
}>>([])
const filterText = ref('')
const showCreateForm = ref(false)
const newBranchName = ref('')
const activeSubview = ref<'branches' | 'worktrees' | 'compare'>('branches')

// ---- Fetch branches ----
async function loadBranches() {
  if (!props.cwd) return
  loading.value = true
  error.value = null
  try {
    const res = await api.executeCodeTool('git_worktree_manager', {
      cwd: props.cwd,
      arguments: { action: 'log', limit: 1 },
    })
    // Parse branch info from status - we use push_plan which has branch info
    const planRes = await api.executeCodeTool('git_worktree_manager', {
      cwd: props.cwd,
      arguments: { action: 'push_plan' },
    })
    const data = (planRes.data ?? {}) as { branch?: string }
    // For branch listing, we'll use git branch command via log action workaround
    // The backend doesn't have a dedicated branch_list action, so we build from worktree info
    const wtRes = await api.executeCodeTool('git_worktree_manager', {
      cwd: props.cwd,
      arguments: { action: 'worktrees' },
    })
    const wtData = (wtRes.data ?? {}) as { worktrees?: Array<Record<string, unknown>> }
    const wtList = Array.isArray(wtData.worktrees) ? wtData.worktrees : []
    const branchList: typeof branches.value = []
    const seen = new Set<string>()
    for (const wt of wtList) {
      const branch = typeof wt.branch === 'string' ? wt.branch : null
      if (branch && !seen.has(branch)) {
        seen.add(branch)
        branchList.push({
          name: branch,
          is_current: wt.is_current === true || branch === data.branch,
          is_remote: false,
          last_subject: typeof wt.commit === 'string' ? wt.commit : undefined,
        })
      }
    }
    // Always ensure current branch is shown
    if (data.branch && !seen.has(data.branch)) {
      branchList.unshift({
        name: data.branch,
        is_current: true,
        is_remote: false,
      })
    }
    branches.value = branchList
  } catch (err) {
    error.value = err instanceof Error ? err.message : t('context.gitLoadFailed')
  } finally {
    loading.value = false
  }
}

watch(() => props.cwd, () => { void loadBranches() }, { immediate: true })

// ---- Computed ----
const filteredBranches = computed(() => {
  if (!filterText.value.trim()) return branches.value
  const lower = filterText.value.toLowerCase()
  return branches.value.filter((b) => b.name.toLowerCase().includes(lower))
})

const localBranches = computed(() => filteredBranches.value.filter((b) => !b.is_remote))
const remoteBranches = computed(() => filteredBranches.value.filter((b) => b.is_remote))

const canCreateBranch = computed(() =>
  Boolean(props.cwd && newBranchName.value.trim() && !props.operationLoading),
)

// ---- Actions ----
function handleCheckout(branch: string) {
  if (branch === props.currentBranch) return
  emit('checkout', branch)
}

function handleCreateBranch() {
  if (!newBranchName.value.trim()) return
  emit('create-branch', newBranchName.value.trim())
  newBranchName.value = ''
  showCreateForm.value = false
}

function onSwitchWorktree(path: string) {
  // Delegate to parent
}

function onCreateWorktree(payload: { branch: string; path: string }) {
  // Delegate to parent
}

function onRemoveWorktree(path: string) {
  // Delegate to parent
}

defineExpose({ refresh: loadBranches })
</script>

<template>
  <div class="git-branch-view">
    <!-- Sub-tabs -->
    <div class="git-branch-subtabs">
      <button
        class="git-branch-subtab"
        :class="{ active: activeSubview === 'branches' }"
        @click="activeSubview = 'branches'"
      >
        <GitBranchIcon :size="13" />
        <span>{{ t('context.gitBranches') }}</span>
      </button>
      <button
        class="git-branch-subtab"
        :class="{ active: activeSubview === 'worktrees' }"
        @click="activeSubview = 'worktrees'"
      >
        <GitPullRequest :size="13" />
        <span>{{ t('context.gitTabWorktrees') }}</span>
      </button>
      <button
        class="git-branch-subtab"
        :class="{ active: activeSubview === 'compare' }"
        @click="activeSubview = 'compare'"
      >
        <span>{{ t('context.gitTabCompare') }}</span>
      </button>
    </div>

    <!-- Branches sub-view -->
    <div v-if="activeSubview === 'branches'" class="git-branch-list-view">
      <div class="git-branch-toolbar">
        <UiInput
          v-model="filterText"
          :placeholder="t('context.gitBranchFilter')"
          size="sm"
        />
        <UiButton variant="ghost" size="xs" :disabled="loading" @click="loadBranches">
          <RefreshCw :size="12" :class="{ spinning: loading }" />
        </UiButton>
        <UiButton variant="secondary" size="xs" @click="showCreateForm = !showCreateForm">
          <Plus :size="12" />
          <span>{{ t('context.gitNewBranch') }}</span>
        </UiButton>
      </div>

      <!-- Create form -->
      <div v-if="showCreateForm" class="git-branch-create-form">
        <UiInput
          v-model="newBranchName"
          :placeholder="t('context.gitNewBranchPlaceholder')"
          size="sm"
          @keyup.enter="handleCreateBranch"
        />
        <div class="git-branch-create-actions">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="!canCreateBranch"
            @click="handleCreateBranch"
          >
            <ShieldCheck :size="13" />
            <span>{{ t('context.gitRequestApproval') }}</span>
          </UiButton>
          <UiButton variant="ghost" size="sm" @click="showCreateForm = false">
            {{ t('context.gitCompareCancel') }}
          </UiButton>
        </div>
        <!-- Execute approved -->
        <div v-if="branchApproval?.status === 'approved'" class="git-branch-execute-row">
          <UiButton variant="secondary" size="sm" :disabled="operationLoading" @click="emit('execute-create-branch')">
            <CheckCircle2 :size="13" />
            <span>{{ t('context.gitExecuteCreateBranch') }}</span>
          </UiButton>
        </div>
        <div v-if="canDecideBranchApproval" class="git-approval-decide">
          <button class="icon-button approve" :title="t('approval.approve')" @click="emit('decide-approval', branchApproval!, 'approved')">
            <CheckCircle2 :size="14" />
          </button>
          <button class="icon-button reject" :title="t('approval.reject')" @click="emit('decide-approval', branchApproval!, 'rejected')">
            <ShieldX :size="14" />
          </button>
        </div>
      </div>

      <!-- Error -->
      <div v-if="error" class="git-branch-error">
        <AlertTriangle :size="13" />
        <span>{{ error }}</span>
      </div>

      <!-- Loading -->
      <div v-if="loading && branches.length === 0" class="git-branch-loading">
        <Loader2 :size="18" class="spinning" />
        <span>{{ t('context.loadingGitPlan') }}</span>
      </div>

      <!-- Branch list -->
      <UiScrollArea v-else class="git-branch-scroll">
        <div v-if="localBranches.length > 0" class="git-branch-group">
          <div class="git-branch-group-title">{{ t('context.gitLocalBranches') }}</div>
          <button
            v-for="branch in localBranches"
            :key="branch.name"
            class="git-branch-row"
            :class="{ current: branch.is_current }"
            @click="handleCheckout(branch.name)"
          >
            <div class="git-branch-row-icon">
              <GitBranchIcon :size="13" />
              <Star v-if="branch.is_current" :size="10" class="git-branch-star" />
            </div>
            <div class="git-branch-row-body">
              <span class="git-branch-row-name">{{ branch.name }}</span>
              <span v-if="branch.last_subject" class="git-branch-row-last">
                {{ branch.last_subject }}
              </span>
            </div>
            <UiBadge v-if="branch.is_current" variant="secondary">{{ t('context.gitWorktreeCurrent') }}</UiBadge>
          </button>
        </div>

        <div v-if="remoteBranches.length > 0" class="git-branch-group">
          <div class="git-branch-group-title">{{ t('context.gitRemoteBranches') }}</div>
          <button
            v-for="branch in remoteBranches"
            :key="branch.name"
            class="git-branch-row"
            :class="{ current: branch.is_current }"
            @click="handleCheckout(branch.name)"
          >
            <GitBranchIcon :size="13" />
            <div class="git-branch-row-body">
              <span class="git-branch-row-name">{{ branch.name }}</span>
            </div>
          </button>
        </div>

        <div v-if="branches.length === 0 && !loading" class="git-branch-empty">
          {{ t('context.gitNoBranches') }}
        </div>
      </UiScrollArea>

      <!-- Checkout approval -->
      <div v-if="checkoutApproval" class="git-branch-checkout-approval">
        <div class="git-branch-checkout-approval-info">
          <ShieldCheck :size="13" />
          <span>{{ checkoutApproval.summary }}</span>
          <UiBadge :variant="checkoutApproval.status === 'approved' ? 'default' : 'secondary'">
            {{ checkoutApproval.status }}
          </UiBadge>
        </div>
        <div v-if="canDecideCheckoutApproval" class="git-approval-decide">
          <button class="icon-button approve" :title="t('approval.approve')" @click="emit('decide-approval', checkoutApproval!, 'approved')">
            <CheckCircle2 :size="14" />
          </button>
          <button class="icon-button reject" :title="t('approval.reject')" @click="emit('decide-approval', checkoutApproval!, 'rejected')">
            <ShieldX :size="14" />
          </button>
        </div>
        <UiButton
          v-if="checkoutApproval.status === 'approved'"
          variant="secondary"
          size="sm"
          :disabled="operationLoading"
          @click="emit('execute-checkout')"
        >
          <CheckCircle2 :size="13" />
          <span>{{ t('context.gitExecuteCheckout') }}</span>
        </UiButton>
      </div>
    </div>

    <!-- Worktrees sub-view -->
    <WorktreeManager
      v-else-if="activeSubview === 'worktrees' && cwd"
      :cwd="cwd"
      :current-path="cwd"
      @switch="onSwitchWorktree"
      @create="onCreateWorktree"
      @remove="onRemoveWorktree"
    />

    <!-- Compare sub-view -->
    <CommitCompare
      v-else-if="activeSubview === 'compare' && cwd"
      :cwd="cwd"
    />
  </div>
</template>

<style scoped>
.git-branch-view {
  display: flex;
  flex-direction: column;
  gap: 8px;
  height: 100%;
  min-height: 0;
}

.git-branch-subtabs {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.git-branch-subtab {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 10px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-muted);
  border-radius: 6px;
  color: var(--text-secondary);
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.12s;
}

.git-branch-subtab:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.git-branch-subtab.active {
  color: var(--text-primary);
  border-color: var(--bg-selected-outline);
  background: var(--bg-selected);
}

.git-branch-list-view {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
  flex: 1;
}

.git-branch-toolbar {
  display: flex;
  gap: 6px;
  align-items: center;
}

.git-branch-create-form {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px;
  border: 1px solid var(--border-muted);
  border-radius: 8px;
  background: var(--bg-secondary);
}

.git-branch-create-actions {
  display: flex;
  gap: 6px;
}

.git-branch-execute-row {
  display: flex;
  gap: 6px;
}

.git-branch-error {
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

.git-branch-loading {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px;
  color: var(--text-muted);
  font-size: 12px;
}

.git-branch-scroll {
  flex: 1;
  min-height: 200px;
}

.git-branch-group {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.git-branch-group-title {
  padding: 6px 8px 2px;
  font-size: 10px;
  font-weight: 700;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.git-branch-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  cursor: pointer;
  text-align: left;
  transition: background 0.1s;
}

.git-branch-row:hover {
  background: var(--bg-hover);
}

.git-branch-row.current {
  background: var(--bg-selected);
}

.git-branch-row-icon {
  position: relative;
  display: flex;
  align-items: center;
  color: var(--text-muted);
}

.git-branch-row.current .git-branch-row-icon {
  color: var(--accent-primary);
}

.git-branch-star {
  position: absolute;
  top: -4px;
  right: -4px;
  color: #d29922;
}

.git-branch-row-body {
  display: flex;
  flex-direction: column;
  gap: 1px;
  flex: 1;
  min-width: 0;
}

.git-branch-row-name {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  font-family: 'Geist Mono', ui-monospace, monospace;
}

.git-branch-row-last {
  font-size: 10px;
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.git-branch-empty {
  padding: 24px 16px;
  text-align: center;
  color: var(--text-muted);
  font-size: 12px;
}

.git-branch-checkout-approval {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px;
  border: 1px solid var(--border-muted);
  border-radius: 8px;
  background: var(--bg-secondary);
}

.git-branch-checkout-approval-info {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-primary);
}

.git-approval-decide {
  display: flex;
  gap: 4px;
}

.spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
