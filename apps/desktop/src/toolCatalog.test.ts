import { describe, expect, it } from 'vitest';
import type { CodeToolExecuteResultDto, ToolDescriptorDto } from './api';
import { codeSuiteTools, languageSupportFromTools, projectTemplatesFromResult } from './toolCatalog';

const tools: ToolDescriptorDto[] = [
  {
    id: 'language_runtime_probe',
    display_name: 'Language Runtime Probe',
    domain: 'programming',
    source: 'code',
    risk: 'read-only',
    requires_approval: false,
    execute_endpoint: '/api/v1/code/tools/language_runtime_probe/execute',
    capabilities: ['runtime.probe', 'runtime.nodejs', 'runtime.rust', 'runtime.python']
  },
  {
    id: 'git_worktree_manager',
    display_name: 'Git Worktree Manager',
    domain: 'programming',
    source: 'code',
    risk: 'git-write',
    requires_approval: true,
    execute_endpoint: '/api/v1/code/tools/git_worktree_manager/execute',
    capabilities: ['git.worktree', 'workspace.isolation']
  },
  {
    id: 'read_file',
    display_name: 'Read File',
    domain: 'programming',
    source: 'codex-rust',
    risk: 'read-only',
    requires_approval: false,
    execute_endpoint: '/api/v1/code/tools/read_file/execute',
    capabilities: ['file.read']
  }
];

describe('tool catalog helpers', () => {
  it('extracts language runtime support from tool capabilities', () => {
    expect(languageSupportFromTools(tools)).toEqual(['nodejs', 'python', 'rust']);
  });

  it('keeps Code suite tools separate from Codex primitive tools', () => {
    expect(codeSuiteTools(tools).map((tool) => tool.id)).toEqual([
      'git_worktree_manager',
      'language_runtime_probe'
    ]);
  });

  it('extracts project template summaries from Code tool results', () => {
    const result: CodeToolExecuteResultDto = {
      tool_id: 'project_templates',
      status: 'completed',
      summary: 'Templates',
      evidence: [],
      requires_approval: false,
      approval_summary: null,
      data: {
        templates: [
          { id: 'rust-cli', name: 'Rust CLI', language: 'rust', package_manager: 'cargo' },
          { id: 'nodejs-vite-vue', name: 'Node.js Vite Vue', language: 'nodejs', package_manager: 'npm' },
          { id: 123, name: 'Invalid', language: 'bad', package_manager: 'bad' }
        ]
      }
    };

    expect(projectTemplatesFromResult(result)).toEqual([
      { id: 'nodejs-vite-vue', name: 'Node.js Vite Vue', language: 'nodejs', package_manager: 'npm' },
      { id: 'rust-cli', name: 'Rust CLI', language: 'rust', package_manager: 'cargo' }
    ]);
  });
});
