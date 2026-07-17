# Decision Log

Per doc 06 (ROA) Part 15, rule 11: if it's not written here, it didn't happen and can be relitigated. One line each; ADR-style for big/contested ones. Owner adds an entry the day a decision is frozen.

## Freezes already made in the planning pack (doc 06 Part 16)

These were frozen before coding started and are recorded here as the seed of the log — not decided in this session, just carried over from the source docs so the log isn't empty on day one.

- **2026-07-17** — Pilot category = cafés; target list = 10 candidate cafés in Sweifieh/Abdoun/Weibdeh. _Why:_ doc 01 Part 1 — fastest learning loop, stamp model already culturally understood. _Source:_ doc 01.
- **2026-07-17** — Navigation: 4-tab + center-QR for the customer app. _Source:_ doc 06 Part 16.
- **2026-07-17** — One active loyalty program per merchant for MVP. _Source:_ doc 04 Part 6 (Loyalty module), doc 06 Part 16.
- **2026-07-17** — Membership is per-merchant, not per-branch. _Source:_ doc 04 Part 8 (`customer_memberships` keyed on `merchant_id`, not `branch_id`).
- **2026-07-17** — Redemption model: two-phase (intent → confirm), 5-minute TTL, online-only confirm. _Source:_ doc 05 §9.8 sample (`expiresAt` ~5 min after intent), doc 06 Part 16.
- **2026-07-17** — Offline policy: queue sales for later sync; block redemptions and refunds while offline. _Source:_ doc 04 Part 6 (Transactions: `SyncOfflineBatch`), doc 06 Part 16.
- **2026-07-17** — Staff model: shared device + roster + PIN login (not per-staff device). _Source:_ doc 05 A7 (`staff/login` with `merchantCode, phoneOrEmail, pin, deviceId`).
- **2026-07-17** — JOD minor units = fils, ×1000, end to end (never float). _Source:_ doc 04 Part 8 global conventions, doc 05 global conventions.

## This session (Phase 0 + Phase 1 foundation build)

- **2026-07-17** — Scope for this build pass is Phase 0 + Phase 1 only (solution skeleton, EF Core wiring, cross-cutting middleware, CI, architecture tests). Identity's real OTP verify/JWT/refresh logic and all other modules' business logic are explicitly deferred to Phase 2+. _Why:_ doc 06's own phased roadmap; avoids building ahead of a frozen contract. _Source:_ doc 06 Parts 12, 16.
- **2026-07-17** — Idempotency middleware ships as a wired-but-no-op shell in Phase 1; real `idempotency_records` persistence lands with the Transactions module in Phase 4. _Why:_ doc 04 assigns `idempotency_records` ownership to the `transactions` schema; building it early would mean a module owning another module's table. _Source:_ doc 04 Part 8.
