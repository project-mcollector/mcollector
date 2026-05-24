# Security Policy

## Supported Versions

| Component | Supported |
|---|---|
| Identity.Api | yes |
| Ingestion.Api | yes |
| Analytics.Api | yes |
| EventProcessor | yes |
| Web (Next.js) | yes |
| SDK (`@mcollector/sdk`) | yes |

Only the latest commit on `main` is actively maintained

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities

Report vulnerabilities privately via one of:

- **GitHub private advisory**: use the "Report a vulnerability" button on the [Security tab](../../security/advisories/new)
- **Email**: noreply@mail.mcollector.publicvm.com — include "SECURITY" in the subject line

Include in your report:

- Affected component and version/commit
- Steps to reproduce or a proof-of-concept
- Potential impact (data exposure, auth bypass, privilege escalation, etc)

You can expect an acknowledgement within **48 hours** and a status update within **7 days**

## Coordinated Disclosure

We follow coordinated disclosure. Please give us **90 days** to investigate and release a fix before publishing your findings. We will credit reporters in the fix commit or release notes unless you prefer to remain anonymous

## Out of Scope

The following are not considered vulnerabilities:

- Rate limit bypass without demonstrated impact (auth: 10 req/min per IP; api: 100 req/min per user ID are intentional limits)
- Self-XSS or issues that require the attacker to already have full account access
- Findings from automated scanners with no proof-of-concept
- `POST /api/v1/ingest/events` accepting requests without a JWT — this endpoint is intentionally unauthenticated; it uses a `writeKey` in the request body resolved to a project ID

## Security Design

| Area | Design |
|---|---|
| Auth tokens | Short-lived JWT access tokens (60 min default) + 64-byte random refresh tokens (30 days default) |
| API keys | Format `proj_<32-byte-base64url>`, stored hashed |
| Passkeys | WebAuthn via ASP.NET Identity (`AddIdentityCore`) |
| Transport | HTTPS enforced in production |
| Input validation | `DataAnnotations` on all inbound models; batch limit of 50 events per request |
| Error handling | Railway-oriented `Result<T>` pattern — domain failures never throw; no stack traces leak to clients |
