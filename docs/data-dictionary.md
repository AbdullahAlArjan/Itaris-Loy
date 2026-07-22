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

### `customers` schema (Phase 3+)

| Table | Purpose | Status |
|---|---|---|
| `customer_profiles` | Customer-specific data incl. shadow profiles; `user_id` (identity link), phone, first_name, gender, preferred_language, birth_date, `is_shadow`, `claimed_at`. Cashier-enrolled phone-only customers start `is_shadow=true` and are claimed on first real registration | Migrated (Customers module) |
| `deletion_requests` | PDPL account-deletion requests (Phase 6); 7-day grace (`execute_after`), status pending/cancelled/executed | Migrated (Phase 6) |

### `transactions` schema (Phase 4)

| Table | Purpose | Status |
|---|---|---|
| `transactions` | Sales; merchant/branch/membership/staff, amount, status (completed/partially_refunded/refunded), refunded_amount, occurred/recorded, source, rule | Migrated (Phase 4) |
| `transaction_items` | Line items — schema-ready, unused by MVP UI (doc 04) | Migrated (Phase 4) |
| `points_ledger_entries` | Immutable source of truth for points/stamps; entry_type, points/stamps delta, balance_after, source_type/id, reason, created_by (append-only) | Migrated (Phase 4) |
| `refunds` | Full/partial refunds; amount, points/stamps clawback, requested_by/approved_by | Migrated (Phase 4) |
| `idempotency_records` | Replay protection (key = client key + route + actor); request_hash, response, expires_at. Also backs single-use QR nonces | Migrated (Phase 4) |

### `rewards` schema (Phase 5)

| Table | Purpose | Status |
|---|---|---|
| `rewards` | Reward catalog; cost_type (points/stamp_completion), points_cost, stock_remaining (null=unlimited), per_customer_limit, valid_from/until, status | Migrated (Phase 5) |
| `redemptions` | Two-phase redemption; status (pending/completed/cancelled/expired), 6-char code, points_held, stamp_card_consumed, expires_at (5-min TTL), confirmed_at. Partial unique index = one pending per (customer, merchant) | Migrated (Phase 5) |

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

### `loyalty` schema (Phase 3)

| Table | Purpose | Status |
|---|---|---|
| `loyalty_programs` | One program per merchant; `type` (points/stamps), status (draft/active/paused), `current_rule_id`. Partial unique index enforces one active program per merchant | Migrated (Phase 3) |
| `loyalty_rules` | Immutable versioned rule snapshots; `config` jsonb (points rate, rounding, min_amount, welcome_bonus, card_size, stamps_per_visit, max_stamps, expiry_months) | Migrated (Phase 3) |
| `customer_memberships` | Customer↔merchant membership with cached balance projection (points_balance, stamps_filled, stamp_card_cycle); unique per (customer, merchant) | Migrated (Phase 3) |

All other tables in doc 04 Part 8 (`customer_profiles`, `merchants`, `branches`, `staff_members`, `roles`, `permissions`, `loyalty_programs`, `transactions`, `points_ledger_entries`, `rewards`, `redemptions`, `refunds`, `idempotency_records`, `audit_logs`, `fraud_flags`, `notifications`, `integration_events`, `pos_providers`, `pos_connections`, `disputes`, `daily_*_stats`, `deletion_requests`) land with their owning module's phase (see doc 06 Part 12) and get an entry here when migrated — not before.
