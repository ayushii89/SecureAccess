# SecureAccess

[![CI](https://github.com/ayushii89/SecureAccess/actions/workflows/ci.yml/badge.svg)](https://github.com/ayushii89/SecureAccess/actions/workflows/ci.yml)

**Live demo:** [frontend-mocha-gamma-16.vercel.app](https://frontend-mocha-gamma-16.vercel.app) · **API + Swagger:** [auth-production-2188.up.railway.app](https://auth-production-2188.up.railway.app)

A multi-tenant authentication & authorization platform — the kind of identity system an enterprise SaaS product needs under the hood, built end-to-end: backend, RBAC engine, audit trail, and admin UI.

## What's in it

- **Multi-tenancy** — every organization's data (users, roles, audit logs) is isolated at the ORM layer via EF Core global query filters scoped to the caller's JWT, not just filtered in application code. Verified with integration tests that register two orgs and assert zero cross-tenant visibility.
- **JWT auth with refresh token rotation** — short-lived access tokens, long-lived refresh tokens that rotate on every use. Reusing an already-rotated (revoked) token is treated as theft and revokes the entire chain.
- **Google OAuth login** — first-time sign-in auto-creates an org, matching the self-serve `/auth/register` flow. Tokens never appear in the OAuth redirect URL: the backend hands back a single-use, 60-second exchange code instead of the JWT itself, which the frontend immediately swaps for real tokens server-side.
- **Policy-based RBAC** — roles map to a permission catalog (`users:create`, `roles:manage`, `audit:read`, ...) checked per-endpoint via `[Authorize(Policy = "...")]`. Four starter roles (Admin/Manager/Developer/Intern) are seeded automatically for every new org.
- **Audit logging** — every security-relevant event (logins, failed logins, role/permission changes, user creation) is recorded and queryable per-tenant.
- **Rate limiting** — per-IP sliding-window limits on `/auth/login`, `/auth/register`, `/auth/refresh` to blunt credential-stuffing and signup abuse.
- **React admin frontend** — role/permission management, user provisioning, and audit log viewer, RBAC-aware (a 403 renders as a clear message, not a broken screen).

## Tech stack

**Backend:** C#, ASP.NET Core 8, Entity Framework Core, PostgreSQL, JWT
**Frontend:** React, TypeScript, Vite
**Infra:** Docker, Railway (API + Postgres), Vercel (frontend)
**Testing:** xUnit + `WebApplicationFactory` — 21 integration tests running the full HTTP pipeline against a live database

## Architecture

```
Organization (tenant)
  └── User ──< UserRole >── Role ──< RolePermission >── Permission
       └── RefreshToken            (per-org, seeded: Admin/Manager/Developer/Intern)
  └── AuditLog
```

`User.PasswordHash` is nullable — Google-only accounts have none and can't fall back to password login.

Every tenant-scoped table (`Users`, `Roles`, `AuditLogs`) carries an `OrganizationId` and an EF Core global query filter keyed off the current request's JWT `org_id` claim — so a stray query without an explicit `WHERE OrganizationId = ...` still can't leak another tenant's data.

## Run it locally

**API:**
```bash
cp src/SecureAccess.Api/appsettings.Development.json.example src/SecureAccess.Api/appsettings.Development.json
# edit the SigningKey in that file, e.g.: openssl rand -base64 32
# Google:ClientId/ClientSecret are optional — password auth works fine without them,
# "Continue with Google" just won't until you add a Google Cloud OAuth Client.

# create a `secureaccess` Postgres database/user matching the connection string above, then:
cd src/SecureAccess.Api
dotnet run
```
Applies pending migrations and seeds the permission catalog automatically. Swagger at `/swagger`.

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```
Opens on http://localhost:5173 — see [frontend/README.md](frontend/README.md).

**Tests:**
```bash
dotnet test
```

## API overview

| Endpoint | Description |
|---|---|
| `POST /auth/register` | Creates an Organization + its first user (Admin role) |
| `POST /auth/login` | Email/password login (rate limited) |
| `GET /auth/google/login` | Starts Google sign-in (rate limited) |
| `POST /auth/google/exchange` | Swaps a one-time OAuth code for real tokens (rate limited) |
| `POST /auth/refresh` | Rotates the refresh token (rate limited) |
| `POST /roles`, `/roles/{id}/permissions`, `/roles/assign` | Manage RBAC (`roles:manage`) |
| `POST /users` | Create users in your org (`users:create`) |
| `GET /audit-logs` | Security event trail, scoped to your org (`audit:read`) |

## Deployment

The API is a single Dockerfile (multi-stage: SDK build → aspnet runtime), deployed on Railway with a linked Postgres addon. The frontend is a static Vite build on Vercel, pointed at the Railway API via `VITE_API_BASE_URL`. Both sides authenticate the browser origin through a `Cors:AllowedOrigins` config entry.

Railway terminates TLS at its edge and forwards plain HTTP to the container, so the API trusts `X-Forwarded-Proto`/`-For` (`ForwardedHeadersOptions`) — without it, the Google OAuth handler builds an `http://` redirect URI that mismatches the `https://` one registered with Google, and the rate limiter's per-IP key ends up tracking the proxy instead of the real client.

## Roadmap (not in this MVP)

GitHub OAuth login, resource-level authorization policies beyond role→permission, Redis session cache.
