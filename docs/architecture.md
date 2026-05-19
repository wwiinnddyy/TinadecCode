# TinadecCode Architecture

TinadecCode is split into three product projects:

- `src/TinadecCore`: independent C# core library. It owns sessions, messages, approvals, model providers, model routing, events, secrets, permissions, and SQLite persistence.
- `apps/gateway`: TinadecCode Elysia BFF/API layer. It exposes `/api/v1/*`, OpenAPI docs at `/docs`, and proxies to the C# core runtime.
- `apps/desktop`: TinadecCode Desktop, built with Electron + Vue. The renderer receives only the `window.tinadec.*` preload API and talks to TinadecCode over HTTP/SSE.

The C# core is the only state authority. TinadecCode/Elysia must not keep session state, approval decisions, model routing state, or provider lifecycle state.

`src/Tinadec.AgentCore` is the local C# runtime wrapper for the core library. It hosts the Core HTTP surface used by TinadecCode during the MVP, but business logic must stay in `src/TinadecCore`.

## Default Ports

- TinadecCode Elysia API: `http://127.0.0.1:48730`
- TinadecCore runtime wrapper: `http://127.0.0.1:48731`
- Vite renderer: `http://127.0.0.1:5173`

## Event Envelope

All runtime events use:

```json
{
  "v": "1.0",
  "type": "message.created",
  "request_id": "req_xxx",
  "session_id": "sess_xxx",
  "trace_id": "trace_xxx",
  "seq": 1,
  "ts": "2026-05-18T10:15:30Z",
  "capabilities": ["agent.message"],
  "payload": {},
  "error": null
}
```

## Run Locally

```powershell
npm install
npm run restore:dotnet
npm run dev
```

The local environment currently contains `Version=V7.24.42SP3`, which breaks MSBuild version parsing. Root npm scripts remove that variable only for the child .NET process.
