# Data Dictionary

Tracks PostgreSQL schema/table state as migrations land, per doc 04 Part 8 (global conventions: UUIDv7 PKs, `timestamptz` UTC `*_at` columns, money as `bigint` minor units suffixed `_minor`, `xmin`-based optimistic concurrency, merchant tenancy via `merchant_id` + EF global query filters).

## Schemas (one per module, doc 04 Part 6/8)

`identity` · `customers` · `merchants` · `loyalty` · `transactions` · `rewards` · `ops` · `reporting`

## Tables present after this migration

### `identity` schema

| Table | Purpose | Status |
|---|---|---|
| `users` | All human identities (customer/staff/owner/admin); Phase 2 added `status`, `failed_login_attempts`, `locked_until`, unique `email` | Migrated (Phase 1–2) |
| `otp_challenges` | OTP lifecycle | Migrated (Phase 1) |
| `refresh_tokens` | Rotating refresh tokens; Phase 2 added `claims` (jsonb snapshot for rotation) | Migrated (Phase 1–2) |
| `devices` | Customer/staff devices, FCM tokens | Migrated (Phase 1) |

### `merchants` schema (Phase 2)

| Table | Purpose | Status |
|---|---|---|
| `merchants` | Tenant root; `code`, bilingual name, category, status, settings jsonb | Migrated (Phase 2) |
| `branches` | Physical locations; geo, area, address, active flag | Migrated (Phase 2) |
| `staff_members` | Employment link user↔merchant; PIN hash + lockout, refund-limit override | Migrated (Phase 2) |
| `roles` | Role templates (seeded system roles; `merchant_id` null = system) | Migrated (Phase 2) |
| `permissions` | Permission-string catalog (seeded) | Migrated (Phase 2) |
| `role_permissions` | Role↔permission map (seeded per doc 01 matrix) | Migrated (Phase 2) |
| `staff_roles` | Staff↔role, optionally branch-scoped | Migrated (Phase 2) |
| `staff_invites` | Staff activation tokens (see decisions.md — moved from `identity` schema) | Migrated (Phase 2) |

### `ops` schema (Phase 2)

| Table | Purpose | Status |
|---|---|---|
| `audit_logs` | Append-only staff/admin action trail written by the EF audit interceptor (actor, entity, action, payload jsonb of changed column names; `merchant_id` null = platform action) | Migrated (Phase 2) |

All other tables in doc 04 Part 8 (`customer_profiles`, `merchants`, `branches`, `staff_members`, `roles`, `permissions`, `loyalty_programs`, `transactions`, `points_ledger_entries`, `rewards`, `redemptions`, `refunds`, `idempotency_records`, `audit_logs`, `fraud_flags`, `notifications`, `integration_events`, `pos_providers`, `pos_connections`, `disputes`, `daily_*_stats`, `deletion_requests`) land with their owning module's phase (see doc 06 Part 12) and get an entry here when migrated — not before.
