export interface CodeToolExecuteRequest {
  session_id?: string | null;
  run_id?: string | null;
  task_node_id?: string | null;
  cwd?: string | null;
  arguments?: Record<string, unknown> | null;
}

export interface CodeToolExecuteResult {
  tool_id: string;
  status: 'stubbed' | 'blocked' | 'failed';
  summary: string;
  evidence: string[];
  data: Record<string, unknown>;
  requires_approval: boolean;
  approval_summary?: string | null;
}

interface ToolSpec {
  id: string;
  summary: string;
  requiresApproval: boolean;
  approvalSummary?: string;
}

const TOOL_SPECS: Record<string, ToolSpec> = {
  search_files: {
    id: 'search_files',
    summary: 'Code-layer file search stub is wired. Codex Rust search_files will replace this implementation.',
    requiresApproval: false
  },
  sandbox_exec: {
    id: 'sandbox_exec',
    summary: 'Code-layer sandbox exec stub is wired. Execution is blocked until Core approval is supplied.',
    requiresApproval: true,
    approvalSummary: 'Run a sandboxed command in the workspace.'
  },
  apply_patch: {
    id: 'apply_patch',
    summary: 'Code-layer apply patch stub is wired. Workspace writes are blocked until Core approval is supplied.',
    requiresApproval: true,
    approvalSummary: 'Apply a patch that may modify workspace files.'
  },
  review_format: {
    id: 'review_format',
    summary: 'Code-layer review formatter stub is wired. Codex Rust review_format will replace this implementation.',
    requiresApproval: false
  }
};

export function listCodeToolIds(): string[] {
  return Object.keys(TOOL_SPECS);
}

export function executeCodeTool(toolId: string, request: CodeToolExecuteRequest = {}): CodeToolExecuteResult | null {
  const spec = TOOL_SPECS[toolId];
  if (!spec) {
    return null;
  }

  const args = request.arguments ?? {};
  return {
    tool_id: spec.id,
    status: spec.requiresApproval ? 'blocked' : 'stubbed',
    summary: spec.summary,
    evidence: [
      'domain: programming',
      'state_owner: core',
      'native_runtime: pending'
    ],
    data: {
      cwd: request.cwd ?? null,
      argument_keys: Object.keys(args).sort()
    },
    requires_approval: spec.requiresApproval,
    approval_summary: spec.approvalSummary ?? null
  };
}
