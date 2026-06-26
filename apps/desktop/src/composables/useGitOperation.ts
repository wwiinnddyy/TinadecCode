import { computed, ref, watch } from 'vue'
import { api, type ApprovalDto, type CodeToolExecuteResultDto } from '../api'
import { useI18n } from 'vue-i18n'

// ---- Type definitions ----

export interface GitStatusFile {
  path: string
  previous_path?: string | null
  staged_status?: string
  unstaged_status?: string
  status?: string
  is_untracked?: boolean
  is_conflicted?: boolean
  is_renamed?: boolean
}

export interface GitDiffSection {
  id: string
  kind: 'working_tree' | 'staged' | 'branch_range' | string
  title: string
  subtitle?: string | null
  base_ref?: string | null
  head_ref?: string | null
  diff: string
  files: Array<{
    path: string
    previous_path?: string | null
    change_type: string
    additions: number
    deletions: number
    binary: boolean
    truncated: boolean
  }>
  file_count: number
  additions: number
  deletions: number
  notices: string[]
}

export interface GitPreviewData {
  git_root?: string
  branch?: string
  upstream?: string | null
  ahead?: number
  behind?: number
  has_uncommitted_changes?: boolean
  files?: GitStatusFile[]
  sections?: GitDiffSection[]
}

export interface GitPushPlanData extends GitPreviewData {
  diff_stat?: string
  recent_commits?: string[]
  remotes?: string[]
  push_ready?: boolean
  push_blockers?: string[]
  suggested_commands?: string[]
  needs_push?: boolean
  worktrees?: Array<Record<string, unknown>>
}

export interface GitLogCommit {
  hash: string
  short_hash: string
  author: string
  email: string
  date: string
  subject: string
}

export interface GitBranch {
  name: string
  is_current: boolean
  is_remote: boolean
  upstream?: string | null
  ahead?: number
  behind?: number
  last_subject?: string
  last_date?: string
}

export interface GitRepoSummary {
  branch: string
  upstream: string | null
  ahead: number
  behind: number
  totalChanges: number
  stagedCount: number
  unstagedCount: number
  untrackedCount: number
}

// ---- Helper functions ----

function stringList(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((entry): entry is string => typeof entry === 'string' && entry.trim().length > 0)
    : []
}

function stringListFromText(value: unknown): string[] {
  return typeof value === 'string'
    ? value.split(/\r?\n/).map((line) => line.trim()).filter(Boolean)
    : []
}

/**
 * Parse a Git status code (e.g. "M", "A", "D", "R", "?", "UU") into a
 * human-readable change type label.
 */
export function statusToLabel(status?: string): string {
  if (!status) return '?'
  const code = status.trim()
  switch (code) {
    case 'M':
      return 'M'
    case 'A':
      return 'A'
    case 'D':
      return 'D'
    case 'R':
      return 'R'
    case 'C':
      return 'C'
    case 'U':
    case 'UU':
    case 'AA':
    case 'DD':
      return 'U'
    case '?':
      return '?'
    case '!':
      return 'I'
    default:
      return code.charAt(0) || '?'
  }
}

/**
 * Map a status code to a color category for UI rendering.
 */
export function statusColor(status?: string): 'added' | 'modified' | 'deleted' | 'renamed' | 'untracked' | 'conflict' | 'ignored' {
  if (!status) return 'untracked'
  const code = status.trim()
  if (code === '?' || code === '!!') return 'untracked'
  if (code === 'A') return 'added'
  if (code === 'D') return 'deleted'
  if (code === 'R' || code === 'C') return 'renamed'
  if (code.startsWith('U') || code === 'AA' || code === 'DD') return 'conflict'
  if (code === '!') return 'ignored'
  return 'modified'
}

// ---- Composable ----

export function useGitOperation(
  currentProjectPath: () => string | undefined,
  selectedSessionId: () => string | null,
  approvals: () => ApprovalDto[],
) {
  const { t } = useI18n()

  // ---- Reactive state ----
  const loading = ref(false)
  const operationLoading = ref(false)
  const error = ref<string | null>(null)
  const feedback = ref<string | null>(null)

  const preview = ref<CodeToolExecuteResultDto | null>(null)
  const pushPlan = ref<CodeToolExecuteResultDto | null>(null)
  const logResult = ref<CodeToolExecuteResultDto | null>(null)
  const branchResult = ref<CodeToolExecuteResultDto | null>(null)

  const commitMessage = ref('')
  const selectedPaths = ref<Set<string>>(new Set())
  const selectAll = ref(true)

  // Approval tracking
  const indexApprovalId = ref<string | null>(null)
  const indexAction = ref<'stage' | 'unstage' | null>(null)
  const commitApprovalId = ref<string | null>(null)
  const pushApprovalId = ref<string | null>(null)
  const pullApprovalId = ref<string | null>(null)
  const checkoutApprovalId = ref<string | null>(null)
  const branchApprovalId = ref<string | null>(null)

  // ---- Computed ----
  const previewData = computed(() => (preview.value?.data ?? {}) as GitPreviewData)
  const pushData = computed(() => (pushPlan.value?.data ?? {}) as GitPushPlanData)

  const statusFiles = computed(() =>
    Array.isArray(previewData.value.files) ? previewData.value.files : [],
  )
  const sections = computed(() =>
    Array.isArray(previewData.value.sections) ? previewData.value.sections : [],
  )
  const totalChanges = computed(() => statusFiles.value.length)

  const recentCommits = computed(() => stringList(pushData.value.recent_commits).slice(0, 10))

  const repoSummary = computed<GitRepoSummary>(() => {
    const files = statusFiles.value
    let staged = 0
    let unstaged = 0
    let untracked = 0
    for (const file of files) {
      if (file.is_untracked || file.status === '?') {
        untracked++
      } else if (file.staged_status && file.staged_status !== ' ' && file.staged_status !== '?') {
        staged++
      } else if (file.unstaged_status && file.unstaged_status !== ' ' && file.unstaged_status !== '?') {
        unstaged++
      } else if (file.status && file.status !== ' ') {
        // Fallback: count as unstaged
        unstaged++
      }
    }
    return {
      branch: previewData.value.branch ?? '-',
      upstream: previewData.value.upstream ?? null,
      ahead: typeof previewData.value.ahead === 'number' ? previewData.value.ahead : 0,
      behind: typeof previewData.value.behind === 'number' ? previewData.value.behind : 0,
      totalChanges: files.length,
      stagedCount: staged,
      unstagedCount: unstaged,
      untrackedCount: untracked,
    }
  })

  const pushBlockers = computed(() =>
    Array.isArray(pushData.value.push_blockers) ? pushData.value.push_blockers : [],
  )
  const pushReady = computed(() => pushData.value.push_ready === true)
  const noUpstreamOnly = computed(
    () => pushBlockers.value.length === 1 && pushBlockers.value[0] === 'no upstream',
  )
  const hasPushCandidate = computed(() => {
    const ahead = typeof pushData.value.ahead === 'number' ? pushData.value.ahead : 0
    return (pushReady.value && ahead > 0) || noUpstreamOnly.value
  })
  const pushCommand = computed(() =>
    noUpstreamOnly.value ? `git push -u origin ${pushData.value.branch ?? 'HEAD'}` : 'git push',
  )

  const selectedCommitPaths = computed(() => [...selectedPaths.value])
  const selectAllIndeterminate = computed(
    () => selectedPaths.value.size > 0 && selectedPaths.value.size < statusFiles.value.length,
  )

  const logCommits = computed<GitLogCommit[]>(() => {
    const data = (logResult.value?.data ?? {}) as { commits?: unknown }
    return Array.isArray(data.commits) ? (data.commits as GitLogCommit[]) : []
  })

  const branches = computed<GitBranch[]>(() => {
    const data = (branchResult.value?.data ?? {}) as { branches?: unknown }
    return Array.isArray(data.branches) ? (data.branches as GitBranch[]) : []
  })

  // Approval lookups
  const allApprovals = computed(() => approvals())
  const indexApproval = computed(
    () => allApprovals.value.find((a) => a.id === indexApprovalId.value) ?? null,
  )
  const commitApproval = computed(
    () => allApprovals.value.find((a) => a.id === commitApprovalId.value) ?? null,
  )
  const pushApproval = computed(
    () => allApprovals.value.find((a) => a.id === pushApprovalId.value) ?? null,
  )
  const pullApproval = computed(
    () => allApprovals.value.find((a) => a.id === pullApprovalId.value) ?? null,
  )
  const checkoutApproval = computed(
    () => allApprovals.value.find((a) => a.id === checkoutApprovalId.value) ?? null,
  )
  const branchApproval = computed(
    () => allApprovals.value.find((a) => a.id === branchApprovalId.value) ?? null,
  )

  const canDecideIndexApproval = computed(() => indexApproval.value?.status === 'pending')
  const canDecideCommitApproval = computed(() => commitApproval.value?.status === 'pending')
  const canDecidePushApproval = computed(() => pushApproval.value?.status === 'pending')
  const canDecidePullApproval = computed(() => pullApproval.value?.status === 'pending')
  const canDecideCheckoutApproval = computed(() => checkoutApproval.value?.status === 'pending')
  const canDecideBranchApproval = computed(() => branchApproval.value?.status === 'pending')

  // ---- Validation computed ----
  const cwd = computed(() => currentProjectPath())
  const sid = computed(() => selectedSessionId())

  const canRequestIndexApproval = computed(() =>
    Boolean(cwd.value && sid.value && selectedCommitPaths.value.length > 0),
  )
  const canRequestCommitApproval = computed(() =>
    Boolean(cwd.value && sid.value && commitMessage.value.trim() && selectedCommitPaths.value.length > 0),
  )
  const canRequestPushApproval = computed(() =>
    Boolean(cwd.value && sid.value && hasPushCandidate.value),
  )
  const canRequestPullApproval = computed(() =>
    Boolean(cwd.value && sid.value && repoSummary.value.behind > 0),
  )

  // ---- Actions ----

  async function loadStatus() {
    const path = cwd.value
    if (!path) {
      preview.value = null
      pushPlan.value = null
      selectedPaths.value = new Set()
      error.value = null
      return
    }
    loading.value = true
    error.value = null
    feedback.value = null
    try {
      const [nextPreview, nextPushPlan] = await Promise.all([
        api.executeCodeTool('git_worktree_manager', {
          cwd: path,
          arguments: { action: 'diff_preview', max_files: 120, max_diff_bytes: 180000 },
        }),
        api.executeCodeTool('git_worktree_manager', {
          cwd: path,
          arguments: { action: 'push_plan' },
        }),
      ])
      preview.value = nextPreview
      pushPlan.value = nextPushPlan
      syncSelection()
    } catch (err) {
      error.value = err instanceof Error ? err.message : t('context.gitLoadFailed')
    } finally {
      loading.value = false
    }
  }

  async function loadLog(limit = 50, ref?: string) {
    const path = cwd.value
    if (!path) return
    try {
      logResult.value = await api.gitLog(path, limit, ref)
    } catch (err) {
      error.value = err instanceof Error ? err.message : t('context.gitLoadFailed')
    }
  }

  async function loadBranches() {
    const path = cwd.value
    if (!path) return
    try {
      const res = await api.executeCodeTool('git_worktree_manager', {
        cwd: path,
        arguments: { action: 'worktrees' },
      })
      branchResult.value = res
    } catch (err) {
      error.value = err instanceof Error ? err.message : t('context.gitLoadFailed')
    }
  }

  async function refreshAll() {
    await Promise.all([loadStatus(), loadLog(), loadBranches()])
  }

  function syncSelection() {
    selectedPaths.value = new Set(statusFiles.value.map((file) => file.path))
    selectAll.value = true
  }

  function togglePath(path: string) {
    const next = new Set(selectedPaths.value)
    if (next.has(path)) {
      next.delete(path)
    } else {
      next.add(path)
    }
    selectedPaths.value = next
    selectAll.value = statusFiles.value.length > 0 && next.size === statusFiles.value.length
  }

  function toggleSelectAll() {
    if (selectAll.value || selectAllIndeterminate.value) {
      selectedPaths.value = new Set()
      selectAll.value = false
    } else {
      selectedPaths.value = new Set(statusFiles.value.map((f) => f.path))
      selectAll.value = true
    }
  }

  function selectAllFiles() {
    selectedPaths.value = new Set(statusFiles.value.map((f) => f.path))
    selectAll.value = true
  }

  // ---- Approval-gated operations ----

  async function requestIndexApproval(action: 'stage' | 'unstage', emitApproval: (a: ApprovalDto) => void) {
    if (!cwd.value || !sid.value || !canRequestIndexApproval.value) return
    operationLoading.value = true
    feedback.value = null
    try {
      const paths = selectedCommitPaths.value
      const isStage = action === 'stage'
      const approval = await api.createApproval({
        session_id: sid.value,
        kind: 'git',
        summary: `${isStage ? 'Stage' : 'Unstage'} ${paths.length} file${paths.length === 1 ? '' : 's'} on ${previewData.value.branch ?? 'HEAD'}`,
        command: `${isStage ? 'git add' : 'git restore --staged'} -- ${paths.join(' ')}`,
        cwd: cwd.value,
      })
      indexApprovalId.value = approval.id
      indexAction.value = action
      feedback.value = t('context.gitIndexApprovalRequested')
      emitApproval(approval)
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitApprovalRequestFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function executeApprovedIndexUpdate() {
    if (!cwd.value || !sid.value || !indexApproval.value || indexApproval.value.status !== 'approved' || !indexAction.value) return
    operationLoading.value = true
    feedback.value = null
    try {
      const confirmKey = indexAction.value === 'stage' ? 'confirm_stage' : 'confirm_unstage'
      const result = await api.executeCodeTool('git_worktree_manager', {
        session_id: sid.value,
        approval_id: indexApproval.value.id,
        cwd: cwd.value,
        arguments: {
          action: indexAction.value,
          [confirmKey]: true,
          paths: selectedCommitPaths.value,
        },
      })
      feedback.value = result.summary
      indexApprovalId.value = null
      indexAction.value = null
      await loadStatus()
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitIndexUpdateFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function requestCommitApproval(emitApproval: (a: ApprovalDto) => void) {
    if (!cwd.value || !sid.value || !canRequestCommitApproval.value) return
    operationLoading.value = true
    feedback.value = null
    try {
      const paths = selectedCommitPaths.value
      const approval = await api.createApproval({
        session_id: sid.value,
        kind: 'git',
        summary: `Commit ${paths.length} file${paths.length === 1 ? '' : 's'} on ${previewData.value.branch ?? 'HEAD'}`,
        command: `git add -- ${paths.join(' ')} && git commit -m "${commitMessage.value.trim()}"`,
        cwd: cwd.value,
      })
      commitApprovalId.value = approval.id
      feedback.value = t('context.gitCommitApprovalRequested')
      emitApproval(approval)
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitApprovalRequestFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function executeApprovedCommit() {
    if (!cwd.value || !sid.value || !commitApproval.value || commitApproval.value.status !== 'approved') return
    operationLoading.value = true
    feedback.value = null
    try {
      const result = await api.executeCodeTool('git_worktree_manager', {
        session_id: sid.value,
        approval_id: commitApproval.value.id,
        cwd: cwd.value,
        arguments: {
          action: 'commit',
          confirm_commit: true,
          paths: selectedCommitPaths.value,
          message: commitMessage.value.trim(),
        },
      })
      feedback.value = result.summary
      commitMessage.value = ''
      commitApprovalId.value = null
      await loadStatus()
      await loadLog()
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitCommitFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function requestPushApproval(emitApproval: (a: ApprovalDto) => void) {
    if (!cwd.value || !sid.value || !canRequestPushApproval.value) return
    operationLoading.value = true
    feedback.value = null
    try {
      const branch = pushData.value.branch ?? 'HEAD'
      const upstream = pushData.value.upstream ?? 'origin'
      const ahead = typeof pushData.value.ahead === 'number' ? pushData.value.ahead : 0
      const approval = await api.createApproval({
        session_id: sid.value,
        kind: 'git',
        summary: `Push ${branch} to ${upstream} (${ahead} ahead)`,
        command: pushCommand.value,
        cwd: cwd.value,
      })
      pushApprovalId.value = approval.id
      feedback.value = t('context.gitApprovalRequested')
      emitApproval(approval)
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitApprovalRequestFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function executeApprovedPush() {
    if (!cwd.value || !sid.value || !pushApproval.value || pushApproval.value.status !== 'approved') return
    operationLoading.value = true
    feedback.value = null
    try {
      const result = await api.executeCodeTool('git_worktree_manager', {
        session_id: sid.value,
        approval_id: pushApproval.value.id,
        cwd: cwd.value,
        arguments: {
          action: 'push',
          confirm_push: true,
          set_upstream: noUpstreamOnly.value,
          remote: 'origin',
        },
      })
      feedback.value = result.summary
      pushApprovalId.value = null
      await loadStatus()
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitPushFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function requestPullApproval(emitApproval: (a: ApprovalDto) => void) {
    if (!cwd.value || !sid.value || !canRequestPullApproval.value) return
    operationLoading.value = true
    feedback.value = null
    try {
      const branch = previewData.value.branch ?? 'HEAD'
      const upstream = previewData.value.upstream ?? 'origin'
      const behind = repoSummary.value.behind
      const approval = await api.createApproval({
        session_id: sid.value,
        kind: 'git',
        summary: `Pull ${branch} from ${upstream} (${behind} behind)`,
        command: 'git pull',
        cwd: cwd.value,
      })
      pullApprovalId.value = approval.id
      feedback.value = t('context.gitApprovalRequested')
      emitApproval(approval)
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitApprovalRequestFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function executeApprovedPull() {
    if (!cwd.value || !sid.value || !pullApproval.value || pullApproval.value.status !== 'approved') return
    operationLoading.value = true
    feedback.value = null
    try {
      const result = await api.executeCodeTool('git_worktree_manager', {
        session_id: sid.value,
        approval_id: pullApproval.value.id,
        cwd: cwd.value,
        arguments: {
          action: 'pull',
          confirm_pull: true,
        },
      })
      feedback.value = result.summary
      pullApprovalId.value = null
      await loadStatus()
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitPullFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function requestCheckoutApproval(branch: string, emitApproval: (a: ApprovalDto) => void) {
    if (!cwd.value || !sid.value) return
    operationLoading.value = true
    feedback.value = null
    try {
      const approval = await api.createApproval({
        session_id: sid.value,
        kind: 'git',
        summary: `Checkout branch ${branch}`,
        command: `git checkout ${branch}`,
        cwd: cwd.value,
      })
      checkoutApprovalId.value = approval.id
      feedback.value = t('context.gitApprovalRequested')
      emitApproval(approval)
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitApprovalRequestFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function executeApprovedCheckout() {
    if (!cwd.value || !sid.value || !checkoutApproval.value || checkoutApproval.value.status !== 'approved') return
    operationLoading.value = true
    feedback.value = null
    try {
      const branch = checkoutApproval.value.command?.replace('git checkout ', '').trim() ?? ''
      const result = await api.executeCodeTool('git_worktree_manager', {
        session_id: sid.value,
        approval_id: checkoutApproval.value.id,
        cwd: cwd.value,
        arguments: {
          action: 'checkout',
          confirm_checkout: true,
          branch,
        },
      })
      feedback.value = result.summary
      checkoutApprovalId.value = null
      await refreshAll()
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitCheckoutFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function requestCreateBranchApproval(branchName: string, emitApproval: (a: ApprovalDto) => void) {
    if (!cwd.value || !sid.value || !branchName.trim()) return
    operationLoading.value = true
    feedback.value = null
    try {
      const approval = await api.createApproval({
        session_id: sid.value,
        kind: 'git',
        summary: `Create and checkout branch ${branchName}`,
        command: `git checkout -b ${branchName}`,
        cwd: cwd.value,
      })
      branchApprovalId.value = approval.id
      feedback.value = t('context.gitApprovalRequested')
      emitApproval(approval)
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitApprovalRequestFailed')
    } finally {
      operationLoading.value = false
    }
  }

  async function executeApprovedCreateBranch() {
    if (!cwd.value || !sid.value || !branchApproval.value || branchApproval.value.status !== 'approved') return
    operationLoading.value = true
    feedback.value = null
    try {
      const branchName = branchApproval.value.command?.replace('git checkout -b ', '').trim() ?? ''
      const result = await api.executeCodeTool('git_worktree_manager', {
        session_id: sid.value,
        approval_id: branchApproval.value.id,
        cwd: cwd.value,
        arguments: {
          action: 'create_branch',
          confirm_create_branch: true,
          branch: branchName,
        },
      })
      feedback.value = result.summary
      branchApprovalId.value = null
      await refreshAll()
    } catch (err) {
      feedback.value = err instanceof Error ? err.message : t('context.gitBranchCreateFailed')
    } finally {
      operationLoading.value = false
    }
  }

  // ---- Utility ----

  function approvalStatusLabel(approval: ApprovalDto | null): string {
    if (!approval) return t('context.gitNoApproval')
    return `${approval.id} / ${approval.status}`
  }

  function decideGitApproval(
    approval: ApprovalDto | null,
    decision: 'approved' | 'rejected',
    decide: (approval: ApprovalDto, decision: 'approved' | 'rejected') => void,
  ) {
    if (!approval || approval.status !== 'pending') return
    decide(approval, decision)
  }

  function resetApprovals() {
    indexApprovalId.value = null
    indexAction.value = null
    commitApprovalId.value = null
    pushApprovalId.value = null
    pullApprovalId.value = null
    checkoutApprovalId.value = null
    branchApprovalId.value = null
  }

  // ---- Watch ----
  watch(
    () => cwd.value,
    () => {
      resetApprovals()
      void loadStatus()
    },
    { immediate: true },
  )

  return {
    // State
    loading,
    operationLoading,
    error,
    feedback,
    commitMessage,
    selectedPaths,
    selectAll,
    selectAllIndeterminate,
    preview,
    pushPlan,
    logResult,
    branchResult,
    // Computed - data
    previewData,
    pushData,
    statusFiles,
    sections,
    totalChanges,
    repoSummary,
    recentCommits,
    logCommits,
    branches,
    pushBlockers,
    pushReady,
    noUpstreamOnly,
    hasPushCandidate,
    pushCommand,
    selectedCommitPaths,
    // Computed - approvals
    indexApproval,
    commitApproval,
    pushApproval,
    pullApproval,
    checkoutApproval,
    branchApproval,
    canDecideIndexApproval,
    canDecideCommitApproval,
    canDecidePushApproval,
    canDecidePullApproval,
    canDecideCheckoutApproval,
    canDecideBranchApproval,
    // Computed - validation
    canRequestIndexApproval,
    canRequestCommitApproval,
    canRequestPushApproval,
    canRequestPullApproval,
    // Actions
    loadStatus,
    loadLog,
    loadBranches,
    refreshAll,
    syncSelection,
    togglePath,
    toggleSelectAll,
    selectAllFiles,
    requestIndexApproval,
    executeApprovedIndexUpdate,
    requestCommitApproval,
    executeApprovedCommit,
    requestPushApproval,
    executeApprovedPush,
    requestPullApproval,
    executeApprovedPull,
    requestCheckoutApproval,
    executeApprovedCheckout,
    requestCreateBranchApproval,
    executeApprovedCreateBranch,
    // Utils
    approvalStatusLabel,
    decideGitApproval,
    resetApprovals,
  }
}
