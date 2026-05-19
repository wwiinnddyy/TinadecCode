# TinadecCode

TinadecCode is a Windows-first intelligent agent desktop workbench for individual developers.

This MVP implements the foundation from the research plan:

- `src/TinadecCore`: independent C# core library and state authority.
- `apps/gateway`: TinadecCode Elysia BFF/API layer.
- `apps/desktop`: TinadecCode Desktop with Electron + Vue.
- Provider-instance based model center for API key, local server, and CLI model access.
- SQLite persistence for projects, sessions, messages, events, and approvals.
- Approval-first shell workflow.

## Start

```powershell
npm install
npm run restore:dotnet
npm run dev
```

OpenAPI docs are available from the gateway at `http://127.0.0.1:48730/docs`.
