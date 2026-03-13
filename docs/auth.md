# Authentication model

Two-app Entra design:

- SPA/public client: signs in user and acquires `user_impersonation` for backend.
- Backend confidential app: validates token and executes delegated operations.

Rules:
- Delegated-first.
- App-only isolated and explicit.
- Exchange/SCC tokens never exposed to browser.
- Tenant context preserved in every job and audit event.
