import assert from 'node:assert/strict';
import test from 'node:test';
import { coreEndpoint } from './coreClient.js';
import { executeCodeTool, listCodeToolIds } from './codeTools.js';

test('coreEndpoint resolves API paths against the configured core URL', () => {
  assert.equal(coreEndpoint('/api/v1/health'), 'http://127.0.0.1:48731/api/v1/health');
});

test('Code tool stubs expose programming-domain execution contracts', () => {
  assert.deepEqual(listCodeToolIds().sort(), ['apply_patch', 'review_format', 'sandbox_exec', 'search_files']);

  const search = executeCodeTool('search_files', { arguments: { query: 'AgentWorkflowRuntime' } });
  assert.equal(search?.requires_approval, false);
  assert.equal(search?.status, 'stubbed');
  assert.deepEqual(search?.data.argument_keys, ['query']);

  const patch = executeCodeTool('apply_patch', { cwd: 'D:/github/TinadecCode' });
  assert.equal(patch?.requires_approval, true);
  assert.equal(patch?.status, 'blocked');

  assert.equal(executeCodeTool('unknown_tool'), null);
});
