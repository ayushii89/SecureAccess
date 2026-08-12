# SecureAccess frontend

Minimal React + TypeScript admin panel for the SecureAccess API: register an organization, log in, manage roles/permissions, create users, and browse the audit log. RBAC-aware — actions the current user's role can't perform surface the API's 403 as a plain-language message instead of a broken screen.

## Run locally

Requires the API running (see the root [README](../README.md)) with its CORS `Cors:AllowedOrigins` including `http://localhost:5173` (already the case in `appsettings.Development.json`).

```bash
npm install
npm run dev
```

Opens on http://localhost:5173. `VITE_API_BASE_URL` in `.env.development` points at the API (defaults to `http://localhost:5080`).

## Structure

- `src/api/` — typed fetch client (`client.ts`), JWT decode for display purposes (`jwt.ts`), DTO types mirroring the backend
- `src/auth/SessionContext.tsx` — session state (access/refresh token, roles) persisted to `localStorage`
- `src/pages/` — `AuthPage` (login/register), `DashboardPage` (tab shell)
- `src/components/` — `RolesPanel`, `UsersPanel`, `AuditLogPanel`

No router or state management library — four views don't need one; `App.tsx` just switches between `AuthPage` and `DashboardPage` based on session presence.
