# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCollector is a data collection and analytics platform. It is a monorepo using **Turborepo** + **pnpm workspaces** with a .NET 10 backend and a Next.js 16 frontend.

## Commands

### Root (all services via Turborepo)
```bash
pnpm install          # install all dependencies
pnpm dev              # run web + all API services in parallel
pnpm build            # build everything
```

### Web (`apps/web`)
```bash
pnpm dev              # Next.js dev server
pnpm build && pnpm start
```

### API (`apps/api`)
```bash
dotnet build
dotnet run --project src/Identity.Api
dotnet run --project src/Ingestion.Api
dotnet run --project src/Analytics.Api
dotnet run --project src/EventProcessor

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/Identity.Api.Tests

# Run a single test by name filter
dotnet test tests/Identity.Api.Tests --filter "FullyQualifiedName~Login"
```

### Infrastructure
```bash
# Start Postgres + Kafka
docker compose -f infrastructure/docker/docker-compose.yml up -d postgres kafka kafka-init
```

### SDK (`packages/sdk`)
```bash
pnpm build   # tsup
pnpm test    # vitest
```

## Architecture

### Service Topology

```
Browser / SDK
    │
    ├─► Identity.Api   (:5003) — auth, users, projects, API key management
    ├─► Ingestion.Api  (:5001) — receives raw events → Kafka
    └─► Analytics.Api  (:5002) — serves processed event data
               ↑
        EventProcessor (worker) — consumes Kafka, writes to DB
```

All four services share a single **PostgreSQL** database. **Kafka** (`raw-events` topic, 3 partitions) decouples ingestion from processing.

### API Services (`apps/api/src/`)

**Identity.Api** — JWT auth (access + refresh tokens), passkeys (WebAuthn via ASP.NET Identity `AddIdentityCore`), email confirmation and password reset via Resend, project CRUD. Projects have a many-to-many relationship with users; members can be added/removed via `ProjectsService`. API keys have the format `proj_<32-byte-base64url>`.

**Ingestion.Api** — two authentication modes:
- `POST /api/v1/ingest/event` and `/batch` — require `X-Project-Id` (GUID) and `X-Api-Key` headers, validated by `ApiKeyAuthenticationHandler` against the Identity DB via `IdentityValidationContext`.
- `POST /api/v1/ingest/events` — `[AllowAnonymous]`; the SDK sends a `writeKey` in the request body, which is resolved to a `projectId` via `IApiKeyValidator.GetProjectIdByApiKeyAsync`. Batch limit is 50 events on both endpoints.

**Analytics.Api** — JWT-authenticated. Routes are under `api/v1/projects/{projectId}/analytics`. Endpoints: `overview`, `events`, `events/counts`, `events/timeseries`, `users/timeseries`. Timeseries accepts `interval` = `hour` | `day` | `month`.

**EventProcessor** — `BackgroundService` consuming Kafka with `EnableAutoCommit = false`. For each message: validates the `RawEvent` with `DataAnnotations`, maps it to `ProcessedEvent` (preferring `ClientTimestamp` over `ServerTimestamp` for the `Timestamp` field, serializing `Properties` to `PropertiesJson`), writes to DB, then commits the offset. Invalid events are dropped with a warning log; duplicate `EventId`s are silently skipped (Postgres unique violation caught).

### BuildingBlocks (`apps/api/src/BuildingBlocks/`)

Shared code referenced as project references by all services:

- **Utilities** — `Result<T>` / `Result` railway-oriented pattern, `Error` record, `Errors` static factory, `IDateTimeProvider`.
- **Infrastructure** — `SharedAuthExtensions` (JWT setup, issuer `mcollector.identity.api`, audience `mcollector.api`); `ApiKeyAuthenticationHandler` (scheme name `"ApiKey"`); Kafka publisher/consumer interfaces; `IRepository<T>`.
- **Contracts** — `RawEvent` / `ProcessedEvent` records shared across services.

### Result Pattern

All service methods return `Result` or `Result<T>`. Never throw for domain failures.

```csharp
// Available extension methods
result.Match(onSuccess, onFailure)
result.Map(value => ...)
result.Bind(value => anotherResultMethod(value))
result.MapAsync(...)
result.BindAsync(...)
result.Ensure(predicate, error)
result.Tap(action)
ResultExtensions.Try(asyncFunc, errorFactory)  // static
```

`Errors` factory: `Unauthorized`, `NotFound`, `Validation`, `Conflict`, `Internal`, `EmailNotConfirmed`.

Controllers check `result.IsSuccess` or call `.Match(...)` to produce `IActionResult`.

### Identity API Layers

```
Api/Controllers/        → thin HTTP layer, maps Result → IActionResult
Application/Services/   → AuthService, TokenService, PasskeyService, ProjectsService, UsersService, ApiKeyService
Domain/Entities/        → ApplicationUser (extends IdentityUser), Project, RefreshToken
Infrastructure/         → IdentityAppDbContext, repositories, health checks
```

`ApiControllerBase` exposes `UserId` and `UserEmail` extracted from JWT claims.

`TokenService` issues access tokens (default 60 min, configurable via `Jwt:ExpiresInMinutes`) and 64-byte random refresh tokens (default 30 days, `Jwt:RefreshTokenDays`). On token creation it purges expired/revoked refresh tokens for the user.

### Rate Limiting (Identity.Api)

- `"auth"` — fixed window, 10 req/min per IP. Applied to unauthenticated auth endpoints.
- `"api"` — sliding window, 100 req/min per user ID. Applied to authenticated endpoints.

Rejected requests get HTTP 429.

### Web (`apps/web/src/`)

Next.js 16 App Router. Two route groups:
- `(auth)` — login, register, confirm-email, forgot/reset-password.
- `(dashboard)` — protected pages (projects, dashboard).

**Auth**: tokens are stored in `localStorage` (`token`, `refreshToken`). `authFetch(url, router, options)` in `lib/auth.ts` attaches `Authorization: Bearer <token>`, intercepts 401s, deduplicates concurrent refresh requests via a single shared `Promise`, and redirects to `/login` if refresh fails.

`BASE_URL` in `lib/constants.ts` is read from `NEXT_PUBLIC_BASE_URL` (falls back to `"https://mcollector.publicvm.com"`).

UI uses shadcn/ui primitives (`src/components/ui/`) with Tailwind CSS v4.

### SDK (`packages/sdk`)

TypeScript package (`@mcollector/sdk`), built with tsup. Exports `analytics` singleton and `Analytics` class. Consumed by the web app and by end-users to send events to Ingestion.Api's `/events` endpoint.

## Configuration

### Required environment variables

| Service | Variable | Purpose |
|---|---|---|
| Identity.Api | `Jwt__Secret` | JWT signing key (≥ 32 chars) |
| Identity.Api | `RESEND_APITOKEN` | Transactional email via Resend |
| Identity.Api | `FrontendUrl` | Base URL for email links (must include protocol) |
| Identity.Api | `Passkey__ServerDomain` | WebAuthn RP domain |
| Analytics.Api | `Jwt__Secret` | Must match Identity.Api value |
| Ingestion.Api | `Kafka__BootstrapServers` | Kafka broker address |
| web | `NEXT_PUBLIC_BASE_URL` | API base URL used by browser |
| web | `RESEND_APITOKEN`, `RESEND_WEBHOOK_SECRET`, `RESEND_FORWARD_TO`, `RESEND_FORWARD_FROM` | Email-forwarding webhook |

Identity.Api reads config from `config/appsettings.json` first, then environment variables (standard `AddEnvironmentVariables()`).

### Local infrastructure

Postgres is exposed on **host port 5433** (container 5432). Local connection string:
```
Host=localhost;Port=5433;Database=mcollector;Username=app;Password=app
```

## Testing

Most tests are **unit tests using Moq** — controllers are instantiated directly with mocked service dependencies and `TestHelpers.ControllerContextFor(userId)` to provide a fake `ClaimsPrincipal`.

Integration test projects (`IngestionControllerIntegrationTests`, `SdkFlowTests`) exist separately. `IdentityApiFactory` extends `WebApplicationFactory<Program>` and swaps Postgres for an EF Core InMemory database and overrides JWT validation keys via `ConfigureTestServices`.

Run a single named test:
```bash
dotnet test tests/Identity.Api.Tests --filter "DisplayName~Login_ValidCredentials"
```