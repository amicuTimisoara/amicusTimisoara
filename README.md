# amicus-api

Backend API for AMiCUS Timișoara, shared by the mobile and web clients.

- **Stack:** .NET 10 (ASP.NET Core) · PostgreSQL 18 · EF Core 10 (Npgsql)
- **Clients:** `amicus-web` (React) and `amicus-mobile` (React Native) — not created yet

## The domain, in one paragraph

Students book short advice appointments with visiting specialists — a lawyer, a
physician, an accountant, a counsellor. Everything hangs off an **`Event`** (a
congress, an advice day) with a start and end date. An **admin** assigns each
specialist's availability as a **`SlotPattern`** ("Tuesdays 14:00–18:00, 30-minute
slots"); specialists do not manage their own time, so a specialist does not even
need an account. Expanding the patterns across the event's date range
materialises **`Slot`** rows, and a student taking one creates a **`Booking`**.

Scoping to events rather than an open-ended weekly timetable is deliberate: there
are no holiday exceptions to model, because events simply end.

### Two decisions worth knowing before you change anything

**The slot board is shared, the bookings are not.** Every student sees which slots
are taken so nobody double-books — but only free/taken and when, never who. Some
of these specialists are physicians and lawyers, so "who is seeing whom" is
readable by that student, their specialist, and admins. Nothing in the public
board response should ever carry a `Booking`.

**Postgres owns "this slot is taken",** via a partial unique index
(`ux_booking_live_slot` on `slot_id WHERE status <> 'Cancelled'`). Two students
tapping the same slot in the same second is a race an application-level "is it
free?" check cannot reliably win, so the loser gets a unique violation to handle.
The filter is what lets a cancelled booking free the slot while its history stays
on the row. Slots are stored rather than computed on the fly precisely so this
index can exist.

Time handling: pattern times are wall-clock in the event's IANA zone, slot
instants are UTC. `SlotPlanner` is pure (no clock, no database) and owns the DST
edges — a start inside the spring-forward gap is skipped rather than silently
moved, and an ambiguous autumn hour resolves to the first pass. Those cases are
tested; read `SlotPlannerTests` before touching it.

## Layout

```
src/Amicus.Domain          entities + SlotPlanner. No dependencies, no EF, no web.
src/Amicus.Infrastructure  EF Core, DbContext, migrations, Identity user/role types.
src/Amicus.Api             ASP.NET Core host, DI wiring, endpoints.
tests/Amicus.Domain.Tests  unit tests for the pure logic.
```

Identity wiring lives in `Amicus.Api`, not Infrastructure: `AddIdentityApiEndpoints`
is part of the ASP.NET Core shared framework, and pulling that into a class library
would make persistence depend on the web stack.

## Running it

```bash
docker compose up -d                 # Postgres on localhost:5433
dotnet tool restore                  # pins dotnet-ef via dotnet-tools.json
dotnet dotnet-ef database update -p src/Amicus.Infrastructure -s src/Amicus.Api
dotnet run --project src/Amicus.Api
```

Port **5433**, not 5432 — the VerseMate stack already claims 5432, and running
both at once should not be a choice you have to make.

`GET /health` round-trips the database, so a green health check means the API can
actually serve, not just that the process started.

### Adding a migration

```bash
dotnet dotnet-ef migrations add <Name> -p src/Amicus.Infrastructure -s src/Amicus.Api
```

## Auth

ASP.NET Core Identity, mounted under `/auth` — `register`, `login`, `refresh`,
`manage/info`. Email + password works today and returns bearer tokens.

Passwords require 10 characters but no symbol classes: students type these on a
phone, and length carries far more real strength than rules that mostly produce
`Pa$$w0rd`.

**Google sign-in is not wired yet.** The package is referenced and Identity stores
external logins in `user_logins`, but the challenge/callback pair still has to be
written. That is the next increment.

## Contributing

`main` is protected: open a PR, get one approval, merge with **squash** (the only
method enabled). Merged branches delete themselves.

CI fails the build on code warnings, but NuGet audit warnings (NU19xx) only warn —
a newly published advisory against a transitive package should not block every open
PR overnight. Dependabot raises those as its own PRs instead.

`Microsoft.OpenApi` is pinned to 2.7.5 on purpose; the reason is in
`Amicus.Api.csproj` next to the pin. Do not "upgrade" it to 3.x.
