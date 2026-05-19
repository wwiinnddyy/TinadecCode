import assert from 'node:assert/strict';
import test from 'node:test';
import { coreEndpoint } from './coreClient.js';

test('coreEndpoint resolves API paths against the configured core URL', () => {
  assert.equal(coreEndpoint('/api/v1/health'), 'http://127.0.0.1:48731/api/v1/health');
});
