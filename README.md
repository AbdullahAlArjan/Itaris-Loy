# Itaris — Backend

Loyalty-program platform for Jordanian merchants (points/stamps, redemptions, POS/cashier flows). Backend-only at this stage; frontend (Flutter apps + merchant dashboard) comes later.

**Stack:** C# / ASP.NET Core · EF Core · PostgreSQL · modular monolith with vertical feature slices · REST + OpenAPI · ASP.NET Core Identity + JWT + rotating refresh tokens · Docker Compose · xUnit + Testcontainers · Serilog · OpenTelemetry · GitHub Actions.

## Status

**Backend core complete (roadmap Phases 0–6).** All eight modules have real content:

- **Identity** — phone OTP login, JWT + rotating refresh with reuse detection, owner/staff/admin login, staff invites.
- **Merchants** — admin-created merchants, seeded roles + permission matrix, staff PIN, admin pause/reactivate.
- **Customers** — profiles, phone-only (shadow) enroll + claim-on-registration, masked lookup, rotating QR, PDPL deletion request.
- **Loyalty** — points/stamps programs with versioned rules, one-active enforcement, memberships, a pure preview calculator.
- **Transactions** — the consistency core: atomic sale + immutable ledger + balance under a membership row lock, full/partial refunds with proportional clawback, real idempotency, QR resolve.
- **Rewards** — reward catalog + stock, two-phase redemption (intent→confirm) with the double-redemption defense, TTL hold release.
- **Ops** — EF audit interceptor + merchant audit-log read.
- **Reporting** — the merchant analytics overview (the 5 numbers).

Cross-cutting: permission-string authorization, global error envelope, OpenAPI/Swagger, Serilog, OpenTelemetry, Docker Compose, GitHub Actions CI. 115 tests (unit / architecture / integration) including the critical concurrency suites (20 concurrent sales, 20 parallel redemption confirms, idempotency replay, out-of-stock race).

**Deferred (post-core / ops):** notifications + FCM, fraud-flag rules, disputes, phone-change, branch CRUD, offline batch sync, and the pilot-prep ops work (VPS deploy, Grafana, Bruno collection, runbook, seed script). See [`docs/decisions.md`](docs/decisions.md) for what's frozen and why.

## Running locally

```
docker compose up
```

Then open `http://localhost:8080/swagger`.

### Demo data (Development only)

On startup in Development (`Seed:Demo=true`, on by default) the API seeds the doc 05 mock world — idempotently — so every flow is clickable with realistic, non-zero data: **Washleh Roasters** (stamps) + **Reef Bakery** (points) + **Weibdeh Café** + **Lamsa Salon**, each with an owner, a cashier, an active program and a reward, plus customers and ~55 days of backdated transactions and a completed redemption.

Sign in with any of these, then click **Authorize** in Swagger and paste the `accessToken`:

| Role | How to log in |
|---|---|
| Platform admin | `POST /v1/auth/admin/login` — `admin@itaris.local` / `dev-admin-pass-change-me` |
| Owner (Washleh) | `POST /v1/auth/owner/login` — `rana@washleh.itaris.local` / `DemoPass123!` |
| Cashier (Washleh) | `POST /v1/auth/staff/login` — merchantCode `WASHLEH`, `omar@washleh.itaris.local`, PIN `1234` |
| Customer (Layla) | `POST /v1/auth/otp/request` then `/verify` — phone `+962790000001`, code `000000` |

Other owners: `reef@itaris.local`, `weibdeh@itaris.local`, `lamsa@itaris.local` (all `DemoPass123!`). See **Reef Bakery**'s analytics (`GET /v1/merchant/analytics/overview`) for non-zero points issued/redeemed.

## Tests

```
dotnet test tests/Itaris.Tests.Unit
dotnet test tests/Itaris.Tests.Architecture
dotnet test tests/Itaris.Tests.Integration   # requires Docker running (Testcontainers)
```
