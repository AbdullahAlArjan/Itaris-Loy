# Data Dictionary

Tracks PostgreSQL schema/table state as migrations land, per doc 04 Part 8 (global conventions: UUIDv7 PKs, `timestamptz` UTC `*_at` columns, money as `bigint` minor units suffixed `_minor`, `xmin`-based optimistic concurrency, merchant tenancy via `merchant_id` + EF global query filters).

## Schemas (one per module, doc 04 Part 6/8)

`identity` · `customers` · `merchants` · `loyalty` · `transactions` · `rewards` · `ops` · `reporting`

## Tables present after this migration

### `identity` schema

| Table | Purpose | Status |
|---|---|---|
| `users` | All human identities (customer/staff/owner/admin) | Migrated (Phase 1) |
| `otp_challenges` | OTP lifecycle | Migrated (Phase 1) |
| `refresh_tokens` | Rotating refresh tokens | Migrated (Phase 1) |
| `devices` | Customer/staff devices, FCM tokens | Migrated (Phase 1) |

All other tables in doc 04 Part 8 (`customer_profiles`, `merchants`, `branches`, `staff_members`, `roles`, `permissions`, `loyalty_programs`, `transactions`, `points_ledger_entries`, `rewards`, `redemptions`, `refunds`, `idempotency_records`, `audit_logs`, `fraud_flags`, `notifications`, `integration_events`, `pos_providers`, `pos_connections`, `disputes`, `daily_*_stats`, `deletion_requests`) land with their owning module's phase (see doc 06 Part 12) and get an entry here when migrated — not before.
