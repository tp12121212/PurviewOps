# Architecture

PurviewOps uses a constrained PowerShell-orchestrated execution model:

1. Frontend (Next.js SPA shell) authenticates user with Entra.
2. Backend API receives typed requests with tenant/user context and correlation ID.
3. Jobs are enqueued for worker execution.
4. Worker selects an allow-listed adapter and runs approved PowerShell cmdlet only.
5. Raw outputs are normalized into deterministic JSON envelopes.

Direct undocumented `/adminapi/beta/.../InvokeCommand` access is not used as a generic public integration pattern.
