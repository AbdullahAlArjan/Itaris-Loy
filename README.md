# Itaris — Backend

Loyalty-program platform for Jordanian merchants (points/stamps, redemptions, POS/cashier flows). Backend-only at this stage; frontend (Flutter apps + merchant dashboard) comes later.

**Stack:** C# / ASP.NET Core · EF Core · PostgreSQL · modular monolith with vertical feature slices · REST + OpenAPI · ASP.NET Core Identity + JWT + rotating refresh tokens · Docker Compose · xUnit + Testcontainers · Serilog · OpenTelemetry · GitHub Actions.

## Status

Phase 0 + Phase 1 foundation: solution skeleton, 8 module shells (only Identity has real content — an OTP-request stub endpoint), EF Core wired to PostgreSQL, global error handling, OpenAPI/Swagger, structured logging, OpenTelemetry baseline, Docker Compose, and the three test projects (unit / integration / architecture). See [`docs/decisions.md`](docs/decisions.md) for what's frozen and why, and [`docs/api-contract.md`](docs/api-contract.md) for the API contract's source of truth.

## Running locally

```
docker compose up
```

Then open `http://localhost:8080/swagger`.

## Tests

```
dotnet test tests/Itaris.Tests.Unit
dotnet test tests/Itaris.Tests.Architecture
dotnet test tests/Itaris.Tests.Integration   # requires Docker running (Testcontainers)
```
