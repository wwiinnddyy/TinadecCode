# TinadecCode Claude Guidance

This file is the Claude-specific entry point for AI agents. It intentionally points back to the shared project memory so Claude, Codex, and other agents follow the same architecture rules.

## Start Here

Before making architecture, feature, UI, or tool-layer changes, read in this order:

1. `AGENTS.md` - shared project memory, commands, conventions, and anti-patterns.
2. `docs/agent-harness-product-model.zh-CN.md` or `docs/agent-harness-product-model.en.md` - product model and layer boundaries.
3. `docs/architecture.md` - current technical architecture and runtime topology.
4. The nearest nested `AGENTS.md` for the area you are changing.
5. The source files and tests that prove current behavior.

For Tool-layer / Code-suite work, inspect:

- `src/TinadecCore/Services/ToolRegistryService.cs` - Core capability registration and approval posture.
- `apps/gateway/src/codeTools.ts` - Code tool catalog, DTO mapping, native execution, and fallback data.
- `apps/desktop/src/toolCatalog.ts` and `apps/desktop/src/pages/SettingsPage.vue` - Desktop presentation of Tool-layer capabilities.

## Product Model

The main mental model is:

- `Core` is the universal agent orchestration model and reusable agent harness.
- `Tool layer` provides executable, approval-aware capabilities.
- `Code` is not a peer layer. It is a built-in code/project/developer-environment tool suite inside the Tool layer.
- `Desktop` is the UI presentation surface for Core state, Tool-layer capabilities, approvals, events, traces, and configuration.

## Boundary Rules

- Core owns sessions, messages, runs, task graphs, approvals, model routes, agent profiles, tool policy, events, traces, and durable state.
- Tool-layer implementations execute capabilities and return structured evidence; they do not own orchestration state or approval policy.
- Code tools such as project templates, bash-like environments, built-in debugging, code editing, and Git worktree management should be registered through the Tool layer and governed by Core.
- Public Tool-layer DTOs use snake_case at the API boundary, even when TypeScript internals use camelCase.
- Desktop must call Gateway, not Core directly.
- Gateway is a proxy and tool bridge. Do not add durable product state there.

## Editing Rules

- Keep `AGENTS.md` and this file aligned when changing the AI reading path or product-layer vocabulary.
- When changing code, configs, commands, ports, contracts, module boundaries, or build/test workflows, update the relevant `AGENTS.md` file in the same work item.
- Do not edit generated or artifact directories: `bin/`, `obj/`, `node_modules/`, `dist/`, `dist-electron/`, `.vite/`, `coverage/`, `output/`, `native/target/`, `tmp/`.
- Preserve the Windows-first workflow. Direct .NET PowerShell commands should clear `Version` and `Ice-Version` first.

## Useful Commands

```powershell
npm run dev
npm run build
npm test
Remove-Item Env:Version -ErrorAction SilentlyContinue
Remove-Item Env:Ice-Version -ErrorAction SilentlyContinue
dotnet test tests/TinadecCore.Tests/TinadecCore.Tests.csproj -v minimal
```
