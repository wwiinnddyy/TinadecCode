import { cors } from '@elysiajs/cors';
import { node } from '@elysiajs/node';
import { swagger } from '@elysiajs/swagger';
import { Elysia } from 'elysia';
import { coreUrl, proxyJson, proxySse } from './coreClient.js';

const port = Number(process.env.TINADEC_GATEWAY_PORT ?? 48730);

function setStatus(set: { status?: number | string }, status: number) {
  set.status = status;
}

const app = new Elysia({ adapter: node() })
  .use(cors({
    origin: [/^http:\/\/127\.0\.0\.1:\d+$/, /^http:\/\/localhost:\d+$/]
  }))
  .use(swagger({
    path: '/docs',
    documentation: {
      info: {
        title: 'TinadecCode API',
        version: '0.1.0'
      }
    }
  }))
  .get('/api/v1/health', async ({ set }) => {
    const result = await proxyJson('/api/v1/health');
    setStatus(set, result.status);
    return { ...result.data, gateway: 'ok', core_url: coreUrl };
  })
  .get('/api/v1/doctor', async ({ set }) => {
    const result = await proxyJson('/api/v1/doctor');
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/projects', async ({ set }) => {
    const result = await proxyJson('/api/v1/projects');
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/projects', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/projects', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions', async ({ query, set }) => {
    const params = new URLSearchParams();
    if (query.project_id) params.set('projectId', String(query.project_id));
    const result = await proxyJson(`/api/v1/sessions?${params.toString()}`);
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/sessions', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/sessions', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions/:sessionId/messages', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/messages`);
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/sessions/:sessionId/messages', async ({ params, body, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/messages`, {
      method: 'POST',
      body: body as Record<string, unknown>
    });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions/:sessionId/orchestration', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/orchestration`);
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions/:sessionId/runs', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/runs`);
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions/:sessionId/task-nodes', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/task-nodes`);
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions/:sessionId/context-packs', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/context-packs`);
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/sessions/:sessionId/supervision-findings', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/sessions/${params.sessionId}/supervision-findings`);
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/events', async ({ query, set }) => {
    const params = new URLSearchParams();
    if (query.session_id) params.set('sessionId', String(query.session_id));
    const response = await proxySse(`/api/v1/events?${params.toString()}`);
    set.headers['content-type'] = 'text/event-stream';
    set.headers['cache-control'] = 'no-cache';
    return response.body;
  })
  .get('/api/v1/approvals', async ({ query, set }) => {
    const params = new URLSearchParams();
    if (query.status) params.set('status', String(query.status));
    if (query.session_id) params.set('sessionId', String(query.session_id));
    const result = await proxyJson(`/api/v1/approvals?${params.toString()}`);
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/approvals', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/approvals', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/approvals/:approvalId/decision', async ({ params, body, set }) => {
    const result = await proxyJson(`/api/v1/approvals/${params.approvalId}/decision`, {
      method: 'POST',
      body: body as Record<string, unknown>
    });
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/tools/shell', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/tools/shell', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/model-provider-templates', async ({ set }) => {
    const result = await proxyJson('/api/v1/model-provider-templates');
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/model-providers', async ({ set }) => {
    const result = await proxyJson('/api/v1/model-providers');
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/model-providers', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/model-providers', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .put('/api/v1/model-providers/:providerInstanceId', async ({ params, body, set }) => {
    const result = await proxyJson(`/api/v1/model-providers/${params.providerInstanceId}`, {
      method: 'PUT',
      body: body as Record<string, unknown>
    });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/model-routes', async ({ set }) => {
    const result = await proxyJson('/api/v1/model-routes');
    setStatus(set, result.status);
    return result.data;
  })
  .put('/api/v1/model-routes/:purpose', async ({ params, body, set }) => {
    const result = await proxyJson(`/api/v1/model-routes/${params.purpose}`, {
      method: 'PUT',
      body: body as Record<string, unknown>
    });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/model-settings', async ({ set }) => {
    const result = await proxyJson('/api/v1/model-settings');
    setStatus(set, result.status);
    return result.data;
  })
  .put('/api/v1/model-settings', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/model-settings', { method: 'PUT', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/market/sources', async ({ set }) => {
    const result = await proxyJson('/api/v1/market/sources');
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/market/sources', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/market/sources', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/market/sources/:sourceId/refresh', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/market/sources/${params.sourceId}/refresh`, { method: 'POST' });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/market/catalog', async ({ query, set }) => {
    const params = new URLSearchParams();
    if (query.kind) params.set('kind', String(query.kind));
    if (query.query) params.set('query', String(query.query));
    if (query.source_id) params.set('sourceId', String(query.source_id));
    const result = await proxyJson(`/api/v1/market/catalog?${params.toString()}`);
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/market/catalog/:catalogId', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/market/catalog/${params.catalogId}`);
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/extensions/install-preview', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/extensions/install-preview', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/extensions/install', async ({ body, set }) => {
    const result = await proxyJson('/api/v1/extensions/install', { method: 'POST', body: body as Record<string, unknown> });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/extensions/installed', async ({ set }) => {
    const result = await proxyJson('/api/v1/extensions/installed');
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/extensions/:extensionId/enable', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/extensions/${params.extensionId}/enable`, { method: 'POST' });
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/extensions/:extensionId/disable', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/extensions/${params.extensionId}/disable`, { method: 'POST' });
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/extensions/:extensionId/update', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/extensions/${params.extensionId}/update`, { method: 'POST' });
    setStatus(set, result.status);
    return result.data;
  })
  .delete('/api/v1/extensions/:extensionId', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/extensions/${params.extensionId}`, { method: 'DELETE' });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/mcp/servers', async ({ set }) => {
    const result = await proxyJson('/api/v1/mcp/servers');
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/mcp/servers/:serverId/tools', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/mcp/servers/${params.serverId}/tools`);
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/mcp/servers/:serverId/reload', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/mcp/servers/${params.serverId}/reload`, { method: 'POST' });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/acp/adapters', async ({ set }) => {
    const result = await proxyJson('/api/v1/acp/adapters');
    setStatus(set, result.status);
    return result.data;
  })
  .post('/api/v1/acp/adapters/:adapterId/probe', async ({ params, set }) => {
    const result = await proxyJson(`/api/v1/acp/adapters/${params.adapterId}/probe`, { method: 'POST' });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/agent-modes', async ({ set }) => {
    const result = await proxyJson('/api/v1/agent-modes');
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/agents', async ({ set }) => {
    const result = await proxyJson('/api/v1/agents');
    setStatus(set, result.status);
    return result.data;
  })
  .put('/api/v1/agents/:agentId', async ({ params, body, set }) => {
    const result = await proxyJson(`/api/v1/agents/${params.agentId}`, {
      method: 'PUT',
      body: body as Record<string, unknown>
    });
    setStatus(set, result.status);
    return result.data;
  })
  .put('/api/v1/agents/:agentId/mode', async ({ params, body, set }) => {
    const result = await proxyJson(`/api/v1/agents/${params.agentId}/mode`, {
      method: 'PUT',
      body: body as Record<string, unknown>
    });
    setStatus(set, result.status);
    return result.data;
  })
  .get('/api/v1/agent-candidates', async ({ set }) => {
    const result = await proxyJson('/api/v1/agent-candidates');
    setStatus(set, result.status);
    return result.data;
  })
  .listen({ port, hostname: '127.0.0.1' });

console.log(`TinadecCode API listening on http://127.0.0.1:${port}`);
