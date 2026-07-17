# API Contract

The OpenAPI document is the single source of truth for endpoint shapes, field names, and auth schemes (doc 05, "Global conventions": camelCase, UTC ISO-8601, money as `{ amountMinor, currency }`, RFC 7807-style errors, cursor pagination, `Idempotency-Key` on marked endpoints).

- **Generated from code**: `Itaris.Api` uses `Microsoft.AspNetCore.OpenApi` to generate the document from the minimal-API endpoint definitions and their typed request/response DTOs — the DTOs are the enforcement point for doc 05's field names, not this file.
- **Exported copy**: `docs/openapi.json` (generated — do not hand-edit; regenerate via the API's `/openapi/v1.json` route or the CI export step once wired).
- **Swagger UI**: available at `/swagger` when the API is running (`docker compose up`).
- **Change policy** (doc 06 Part 15, rule 3): API field names change only via a PR touching the endpoint DTOs + a `docs/decisions.md` entry + a ping to the designer. No silent renames.

## Phase 1 status

Only the auth OTP-request stub (`POST /v1/auth/otp/request`, doc 05 A1) is implemented, returning the shaped stub response. Every other endpoint in doc 05 (§9.1–9.8) is Phase 2+ and not yet present in the generated document.
