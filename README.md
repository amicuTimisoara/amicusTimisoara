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

## Database and SQL — how it actually works

**Code-first EF Core migrations. No hand-written SQL, no schema tool, no
`.sql` files to keep in step.**

The C# entities in `Amicus.Domain` plus the Fluent configuration in
`AmicusDbContext.OnModelCreating` *are* the schema definition. The cycle:

1. Change an entity or its configuration.
2. `dotnet dotnet-ef migrations add <Name> -p src/Amicus.Infrastructure -s src/Amicus.Api`
   EF diffs the model against `AmicusDbContextModelSnapshot.cs` and writes a
   migration class with `Up()` and `Down()`, plus an updated snapshot.
3. Review the generated migration. **It is normal code and it is committed** —
   `src/Amicus.Infrastructure/Migrations/` is part of the repo and reviewed in the PR.
4. `dotnet dotnet-ef database update -p src/Amicus.Infrastructure -s src/Amicus.Api`
   applies whatever has not run yet and records it in the `__EFMigrationsHistory`
   table, which is how the database knows where it is.

Rules that matter:

- **Never edit a migration that has already been applied anywhere.** Add a new
  one. The snapshot is the diff baseline, so hand-editing one file and not the
  other produces migrations that generate nothing, or the wrong thing.
- **Read what EF generated before committing it.** A rename looks like a
  drop-plus-add to the differ, which silently discards data.
- `Down()` is generated for free but rarely exercised. Do not rely on it in
  production; roll forward.
- Constraints and indexes belong in `OnModelCreating`, not in a manual script, so
  they travel with the model. That is how `ck_slot_pattern_window_ordered` and the
  partial index `ux_booking_live_slot` came to exist.
- Migrations are **not** applied automatically at startup. Applying them is a
  deliberate step, so a rolling deploy cannot have two versions racing to migrate.

To see the SQL without touching a database:

```bash
dotnet dotnet-ef migrations script -p src/Amicus.Infrastructure -s src/Amicus.Api
```

Queries are LINQ, translated by Npgsql. `UseSnakeCaseNamingConvention()` maps
`StartsAt` to `starts_at`, so the schema reads like ordinary Postgres.

## Auth

ASP.NET Core Identity, mounted under `/auth` — `register`, `login`, `refresh`,
`manage/info`. Email + password works today and returns bearer tokens.

Passwords require 10 characters but no symbol classes: students type these on a
phone, and length carries far more real strength than rules that mostly produce
`Pa$$w0rd`.

### Google sign-in

**Client-side ID-token flow, not a server redirect.** The web SPA and both mobile
platforms obtain an ID token from Google's own SDK and `POST` it to
`/auth/google`, which verifies it and returns the same `AccessTokenResponse` as
`/auth/login`. No redirect URIs, no deep links, no custom URL schemes, and one
code path for every client.

```jsonc
// appsettings, or user-secrets / environment in production
"Authentication": {
  "Google": {
    // Web, iOS and Android are separate OAuth clients in Google Cloud but one
    // account here, so every client ID that may mint tokens has to be listed.
    "ClientIds": [ "1234-web.apps.googleusercontent.com" ]
  }
}
```

An **unverified** Google email is refused: accounts are matched by address, so
honouring one would let anyone who edits their Google profile email take over
somebody else's account. A verified address that already has a password account
gets **linked** rather than duplicated, so signing in with Google later does not
lock a student out of the account they registered.

### Becoming an admin

No default credentials ship anywhere. Register normally, add the address to
`Bootstrap:AdminEmails`, restart — startup promotes the existing account.

## Endpoints

| | |
|---|---|
| `POST /auth/register` · `login` · `refresh` · `manage/info` | email + password |
| `POST /auth/google` | exchange a Google ID token for ours |
| `GET /events` · `GET /events/{slug}` | published events and their specialists |
| `GET /events/{slug}/board` | the shared board — free/taken and when, never who |
| `POST /bookings` · `GET /bookings/mine` · `POST /bookings/{id}/cancel` | a student's own bookings |
| `POST /check-in` | scan a QR code (Specialist or Admin only) |
| `POST /admin/...` | events, specialists, rosters, patterns, slot generation, publish |

`POST /admin/events/{id}/generate-slots` is safe to re-run: existing slots are
left alone, and a slot no longer produced by any pattern is removed **only** if
nobody ever booked it.

## Tests

```bash
docker compose up -d && dotnet test
```

`Amicus.Domain.Tests` is pure and needs nothing. `Amicus.Api.Tests` hosts the real
app against a **real Postgres** — it creates an `amicus_test` database beside the
dev one and truncates between tests. Point it elsewhere with
`AMICUS_TEST_POSTGRES` (CI uses a service container).

There is no in-memory provider anywhere on purpose: the double-booking guard is a
Postgres partial index, and a fake provider would let those tests pass while
production stayed broken.

## Contributing

`main` is protected: open a PR, get one approval, merge with **squash** (the only
method enabled). Merged branches delete themselves.

CI fails the build on code warnings, but NuGet audit warnings (NU19xx) only warn —
a newly published advisory against a transitive package should not block every open
PR overnight. Dependabot raises those as its own PRs instead.

`Microsoft.OpenApi` is pinned to 2.7.5 on purpose; the reason is in
`Amicus.Api.csproj` next to the pin. Do not "upgrade" it to 3.x.
