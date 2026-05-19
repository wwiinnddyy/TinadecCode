# MVP Security Notes

- Electron runs with `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`, and a minimal preload API.
- Renderer code does not receive model API keys or direct filesystem/shell access.
- API keys are stored by the C# core as protected data on Windows through DPAPI.
- Default tool posture is approval-first. Shell requests create approval records instead of executing immediately.
- Logs and API responses never return the stored API key; they only expose `has_api_key`.

## Not In MVP

- Enterprise policy center.
- Remote browser sessions with login state.
- Plugin marketplace trust and signing.
- Full workspace sandbox execution.
