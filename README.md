# Product Catalog

A product catalogue REST API and React SPA: JWT authentication with refresh-token rotation,
role-based authorization, cache-aside reads, soft deletes, and an RFC 7807 error contract.

**Backend:** .NET 10 · EF Core 10 · Npgsql 10 · PostgreSQL 17 · Redis 7 · Serilog · xUnit
**Frontend:** React 19 · TypeScript · Vite · TanStack Query · Vitest · Playwright

---

## Running it locally — 5 steps

One dependency: **Docker Desktop** with Compose v2. Nothing else is installed on the host — no
.NET SDK, no Node, no PostgreSQL, no Redis.

| # | Step |
|---|---|
| 1 | Install Docker Desktop and make sure it is running (`docker compose version` should print v2.x). |
| 2 | Clone the repository and `cd` into it. |
| 3 | `cp .env.example .env` — on Windows PowerShell, `Copy-Item .env.example .env` |
| 4 | Open `.env` and set `JWT_SIGNING_KEY` to 32+ bytes of random data — `openssl rand -base64 48` |
| 5 | `docker compose up --build -d --wait` — returns only once every service is healthy |

Then open **<http://localhost:5173>**.

| | URL |
|---|---|
| SPA | <http://localhost:5173> |
| API | <http://localhost:8080> |
| Swagger UI | <http://localhost:8080/swagger> |
| Health | <http://localhost:8080/health/ready> |

Compose starts Postgres and Redis, waits for both to pass their health checks, then starts the
API, which applies migrations and seeds 64 products (one of them soft-deleted, so you can see the
query filter working). The SPA starts last, once the API reports healthy. First build takes a few
minutes; subsequent starts are seconds.

`docker compose down` stops everything; add `-v` to drop the database volume as well.

### Demo accounts

| Email | Password | Role |
|---|---|---|
| `admin@demo.local` | `Admin#2026Demo` | Admin — can delete |
| `user@demo.local` | `User#2026Demo` | User — can create and update |

Reads are anonymous, so the catalogue browses without signing in; the demo accounts above are
rendered as dev-only hints on the login screen.

---

## Repository layout

```
backend/     .NET solution — Catalog.slnx, src/, tests/, build props
frontend/    React SPA — Vite, TypeScript, TanStack Query
.github/workflows/ci-cd.yml        build, test, publish, deploy
docker-compose.yml, .env.example   repo-level, orchestrate both
docker-compose.deploy.yml          the same stack, from published images
```

Compose runs the whole stack: Postgres, Redis, the API, and the SPA behind nginx on port 5173 —
already an allowed CORS origin, so the two halves need no extra wiring.

Everything scoped to the API — including `global.json`, `Directory.Build.props`, and
`coverlet.runsettings` — lives under `backend/`, so the two stacks never share a build file.

---

## Working on it

### Frontend with hot reload

The `web` container serves the production bundle through nginx, so there is no HMR. For day-to-day
frontend work there is one command that starts the database, cache and API in Docker and the SPA on
the host:

```bash
./dev.ps1          # Windows PowerShell
./dev.sh           # bash / zsh — Git Bash, WSL, macOS, Linux
```

It copies both `.env` files if they are missing, `npm install`s on first run, stops the `web`
container (it holds port 5173, which is the one origin the API allowlists), waits for the API to
report healthy, then runs the Vite dev server in the foreground. Ctrl+C stops the dev server;
`./dev.ps1 -Stop` / `./dev.sh --stop` stops the containers.

Equivalent by hand:

```bash
docker compose stop web
docker compose up -d --wait postgres redis api
cd frontend && npm run dev
```

### Backend without Docker

```bash
cd backend
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/Catalog.Api
dotnet run --project src/Catalog.Api
```

Needs .NET SDK 10 and a PostgreSQL on `localhost:5432`. Redis is optional — with `Redis:Connection`
blank the API falls back to an in-process `IDistributedCache` behind the same `ICacheService`, so
the caching code path is identical either way.

### Tests

```bash
cd backend
dotnet test                          # all three projects
./coverage.ps1     # or ./coverage.sh — all three, merged into one coverage report

cd ../frontend
npm run test:run    # Vitest
npm run e2e         # Playwright — needs the stack up
```

| Project | Count | Needs Docker | Covers |
|---|---:|---|---|
| `Catalog.UnitTests` | 200 | no | Domain invariants, handlers, validators, paging, ETag, LIKE escaping, controllers, the exception middleware, log redaction |
| `Catalog.ArchitectureTests` | 9 | no | Dependency direction, sealed handlers, no public entity setters |
| `Catalog.IntegrationTests` | 76 | **yes** (Testcontainers Postgres) | Every endpoint, the error contract, rotation, roles, cache invalidation |

Integration tests **skip rather than fail** when no Docker daemon is reachable. A red run on a
machine without Docker trains people to ignore red runs.

#### Coverage

`./coverage.ps1` runs all three suites and merges their reports, because each one measures a
different slice: the unit suite covers Domain, Application and Api; the integration suite covers
Infrastructure. Quoting a single report would be quoting a fraction of the solution.

| Assembly | Line | Branch |
|---|---:|---:|
| `Catalog.Api` | 96.7% | 84.6% |
| `Catalog.Application` | 100% | 94.4% |
| `Catalog.Domain` | 94.3% | 93.5% |
| `Catalog.Infrastructure` | 97.4% | 75.0% |
| **Solution** | **97.0%** | **87.0%** |

`Program.cs`, `Migrations/` and `AppDbContextFactory` are excluded — composition-root wiring,
generated code, and a design-time entry point respectively. See `coverlet.runsettings`.

Branch coverage sits below line coverage, and the gap is worth explaining rather than rounding
off. There are only 184 branches in the whole solution, so each uncovered one costs about half a
percentage point and a handful of misses moves the figure a long way. Most of what remains is the
null arm of a `?.` or `??` on a path where the value cannot be null inside a live request —
`RemoteIpAddress` behind Kestrel, a `TraceIdentifier` Kestrel always assigns — plus
`ApiVersionParameterFilter` and `DatabaseSeeder`, which are Swagger presentation and demo data.
Driving those to 100% would mean writing tests that assert the framework's behaviour rather than
this application's.

The figures come from a run with Docker available. **Without it the integration tests skip and
Infrastructure reads far lower**, which is a fact about the run, not about the code.

---

## Architecture

```
Catalog.Domain          → (nothing)
Catalog.Application     → Domain
Catalog.Infrastructure  → Application, Domain
Catalog.Api             → all three (composition root only)
```

Enforced by `LayerDependencyTests` — nine NetArchTest assertions that fail the build if a
reference points the wrong way, if EF Core appears in Domain or Application, if `HttpContext`
leaks below the API layer, or if an entity grows a public setter.

Feature logic is organised as **vertical slices**: one folder per use case containing its
request, handler, and validator. With one aggregate, an `IProductService → IProductRepository →
DbContext` chain would add three files and zero information.

```
backend/src/Catalog.Application/Features/
├── Auth/{Register,Login,Refresh,Logout}/
└── Products/{CreateProduct,UpdateProduct,DeleteProduct,GetProductById,ListProducts,SearchProducts}/
```

---

## Endpoints

| Method | Route | Auth | Success | Cached |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | anonymous | 201 | — |
| `POST` | `/api/v1/auth/login` | anonymous | 200 | — |
| `POST` | `/api/v1/auth/refresh` | anonymous † | 200 | — |
| `POST` | `/api/v1/auth/logout` | authenticated | 204 | — |
| `GET` | `/api/v1/auth/me` | authenticated | 200 | — |
| `GET` | `/api/v1/products` | anonymous | 200 | pages 1–3, 5 min |
| `GET` | `/api/v1/products/search` | anonymous | 200 | **no** ‡ |
| `GET` | `/api/v1/products/{id}` | anonymous | 200 + `ETag` | 5 min |
| `POST` | `/api/v1/products` | `User` or `Admin` | 201 | invalidates |
| `PUT` | `/api/v1/products/{id}` | `User` or `Admin` | 204 | invalidates |
| `DELETE` | `/api/v1/products/{id}` | **`Admin` only** | 204 | invalidates |
| `GET` | `/health/live`, `/health/ready` | anonymous | 200 | — |

† `/auth/refresh` must be anonymous. The client calls it precisely because its access token has
expired; requiring one would defeat the purpose. The refresh token is the credential.

‡ Search is deliberately uncached — `name` is free text, so the key space is unbounded and a
naive cache-aside fills Redis with single-use keys that evict the entries which actually help.

### Error contract

Every non-2xx response is `application/problem+json`:

```jsonc
{
  "type": "https://datatracker.ietf.org/doc/html/rfc9110#section-15",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/products",
  "code": "validation_failed",     // stable — switch on this, never on `title`
  "traceId": "00-8a3d1f…-01",      // matches the X-Correlation-Id response header
  "errors": { "page": ["page must be 1 or greater."] }
}
```

`traceId` is the same value as the `X-Correlation-Id` response header and the `CorrelationId`
property on every log line for that request. A user quoting the id from a failed response
resolves to exactly one log scope. Supply your own `X-Correlation-Id` to trace across the SPA
and the API.

Codes: `validation_failed`, `malformed_json`, `invalid_credentials`, `missing_token`,
`invalid_token`, `token_expired`, `refresh_token_invalid`, `insufficient_role`,
`resource_not_found`, `email_already_registered`, `concurrency_conflict`, `rate_limited`,
`internal_error`.

---

## Decisions worth explaining

### `xmin` instead of a `rowversion` column

PostgreSQL gives every row a system column `xmin` — the transaction id of its last writer.
Npgsql maps it to a `uint` concurrency token at **zero storage cost** with **server-maintained**
semantics. A `bytea rowversion` is a SQL Server idiom that Npgsql does not maintain for you; you
would have to increment it by hand, which is exactly the kind of thing that silently stops
working.

The scaffolded migration lists `xmin` in `CreateTable` because it is a mapped property, but the
generated DDL contains no such column. Verified:

```bash
cd backend
dotnet ef migrations script --project src/Catalog.Infrastructure --startup-project src/Catalog.Api
# CREATE TABLE catalog.products has nine columns and no xmin
```

Do not "fix" the scaffold by hand — that desynchronises the model snapshot and the next
migration tries to add the column for real.

### Normalised `Email` instead of `citext`

Normalising (trim + lower, invariant) inside the `Email` value object makes uniqueness a plain
unique index on a plain `varchar(256)`. It avoids requiring the `citext` extension — restricted
on some managed Postgres tiers — keeps the schema portable, and makes the invariant testable
without a database. Using both would be two sources of truth for one rule.

### Cache invalidation without a wildcard delete

`IDistributedCache` has no wildcard delete, and `KEYS *` on a real Redis is not an option.
Instead the list-cache key embeds a monotonic generation counter (`products:v{n}:page:…`), and
every write bumps it — orphaning all previous list keys at once. The orphans age out on their own
TTL.

The bump writes `max(current + 1, UtcNow.Ticks)` rather than a plain increment: read-modify-write
across N replicas can lose an update, and anchoring to a clock means the generation still moves
forward when it does. A lost update can never resurrect a stale key.

### `Product.CreatedBy` has no foreign key

It is a bare `Guid?` — no navigation property, no `HasOne`, no cross-schema constraint. This is
the single choice that keeps a future extraction of the Identity module to its own service a
contained piece of work. It costs one thing: the database cannot enforce that the value points at
a real user. That trade is accepted deliberately.

`CreatedBy` is also never exposed on the wire. Leaking user GUIDs to anonymous callers is a
gratuitous information disclosure.

### Data Annotations, and what they cannot do

Requirement F4 asks for Data Annotations, and every request DTO carries them. Two notes:

1. They are on **init-only records**, not positional ones. On a positional record the attribute
   binds to the constructor *parameter*, and MVC's validation metadata provider — which reads
   *property* attributes — validates nothing at all. This is a silent failure and it is why the
   DTOs are more verbose than they could be.
2. They structurally cannot express a relationship between two fields. `minPrice <= maxPrice`
   lives in the `PriceRange` value object, surfaced through a FluentValidation rule that runs in
   the same filter and produces the same error envelope. The requirement says *use* Data
   Annotations, not *only* use them.

### Login is not an enumeration oracle

An unknown email still runs a BCrypt verification against a fixed dummy hash before returning.
Without it the endpoint answers "unknown email" in about a millisecond and "wrong password" in
about 250 — a free account-enumeration oracle. The 401 body is byte-identical for both.

**Registration is a deliberate exception.** Returning 409 on a duplicate email does tell an
attacker which addresses are registered. The privacy-preserving alternative is 202 plus an
out-of-band email, which needs a mail transport this build does not have. 409 is chosen for
usability, and the trade-off is stated rather than silently accepted.

### Refresh-token rotation with reuse detection

```
login ──► T1
   ├─ refresh(T1) ─► T1 revoked "rotated", T1.ReplacedByTokenHash = hash(T2), issue T2
   │                 T2.ExpiresAt = T1.ExpiresAt   (absolute — rotation never extends a session)
   ├─ refresh(T1) again ─► already revoked ⇒ REUSE DETECTED
   │                       ⇒ revoke every active token for the user, log Warning, 401
   └─ logout(T2) ─► T2 revoked "logout"
```

Revoke and issue happen in one `SaveChangesAsync`. A crash between them would otherwise revoke
the user's only token and log them out.

Only the SHA-256 hash of a refresh token is persisted; the plaintext is returned exactly once. A
database dump must not be sufficient to impersonate a session.

### Fallback authorization policy

```csharp
.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
```

A newly added controller is protected unless someone explicitly opts out, so a forgotten
attribute fails **closed** (401) rather than open. That inverts the risk of the single most
common authorization bug in ASP.NET Core codebases, at the cost of six `[AllowAnonymous]`
attributes.

### Runtime seeder, not `HasData`

Bogus produces different values on every run, so `HasData` would fill every subsequent
`migrations add` with `UpdateData` noise. It also needs explicit primary keys, which
`GENERATED ALWAYS AS IDENTITY` will not accept without leaving the sequence out of step — so the
first real `POST /products` would fail on a duplicate key. Seed data is a deployment concern, not
schema.

The seeder is deterministic (`Randomizer.Seed = new Random(20260815)`), idempotent, and off by
default in Production. Integration tests do **not** use it: a test that depends on demo data is a
test that breaks when the demo data changes.

---

## CI/CD

[`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml) runs on every push to `main` and every
pull request against it. The two build jobs are independent and run in parallel, both gated on a
fast secret-hygiene check:

| Job | Runner | Does |
|---|---|---|
| `secrets` | `ubuntu-latest` | fails the run if any credential-bearing file is tracked in git |
| `backend` | `ubuntu-latest` | restore → build → `dotnet test` (all three suites) → merge coverage → build image → push |
| `frontend` | `ubuntu-latest` | `npm ci` → lint → `vitest run` → `vite build` → build image → push |
| `deploy` | `self-hosted` | pull both images, `docker compose up -d`, wait for `/health/ready` |

The backend job publishes a merged coverage report as a run artifact and writes its summary to the
job summary page, so the number in this README can be checked against the run that produced it
rather than taken on trust.

NuGet packages and npm modules are cached between runs, as are the Docker build layers (via the
Actions cache backend). Images go to this repository's GHCR namespace as
`ghcr.io/<owner>/<repo>/catalog-api` and `…/catalog-web`, tagged with both the commit SHA and
`latest`. Pull requests build the images but never push them, so a broken `Dockerfile` fails the PR
without publishing anything.

The integration suite uses Testcontainers, and its `DockerFactAttribute` **skips** rather than fails
when no Docker daemon is reachable — a green run on a container-less runner would mean nothing. The
job therefore stays on `ubuntu-latest`, and uploads a `.trx` artifact so the skip count is visible.

### Deploying to local Docker

> **There is no public deployment of this project, and the `deploy` job does not create one.**
> It targets a **self-hosted runner on the maintainer's own machine** and brings the stack up on
> that machine's Docker daemon — `http://localhost:8080`, reachable from that host and nowhere
> else. Unless you have registered such a runner against your fork, the job will simply queue with
> no runner to take it, which is the expected behaviour and not a broken pipeline.
>
> To see the application running, run it yourself — it is two commands, and the
> [Running it locally](#running-it-locally--5-steps) section at the top is the whole procedure.
> A hosted demo was scoped out rather than half-built: it needs a cloud account, a managed
> Postgres, and a TLS certificate to be worth linking to, and none of those are part of what this
> project sets out to show.

`deploy` runs only on `main`, and only on a **self-hosted runner** — a GitHub-hosted one cannot
reach a local Docker daemon. To set it up:

1. Install a runner on the target machine (Settings → Actions → Runners) with the `self-hosted`
   label. It needs Docker, Compose v2, and — on Windows — the Git Bash that ships with Git for
   Windows.
2. Add the repository secret **`JWT_SIGNING_KEY`** (32+ bytes; `openssl rand -base64 48`), and
   optionally `POSTGRES_PASSWORD`. `GITHUB_TOKEN` is automatic and is what authenticates to GHCR.
3. Create an environment named `local` if you want a manual approval gate on deploys.

The job runs [`docker-compose.deploy.yml`](docker-compose.deploy.yml), which is the same stack as
`docker-compose.yml` but pulls published images instead of building.

`VITE_API_BASE_URL` is baked into the frontend image at build time, because Vite inlines
`import.meta.env.*` statically; it cannot be changed when the container starts. Changing the API URL
means rebuilding the image with a different `--build-arg`.

---

## Security notes

**Token storage in the SPA.** The access token belongs in memory (React context), never in
`localStorage`, which is readable by any injected script and is the standard XSS token-theft path.
The refresh token belongs in an `HttpOnly; Secure; SameSite=Strict` cookie when the SPA is
same-site with the API.

This build returns both tokens in the response body and leaves storage to the client. The
defensible fallback is both in memory, with the user re-authenticating after a hard refresh. It
is stated here rather than left implicit — an unstated token-storage model reads as ignorance.

**Rate limiting behind a proxy.** The login and register limiters partition on
`RemoteIpAddress`. Behind a reverse proxy that is the *proxy's* address unless
`UseForwardedHeaders` is configured with `KnownProxies`, in which case the limit becomes global
and the limiter is theatre. Configure it before deploying behind one.

**Secrets.** `appsettings*.json` contains no secrets and is committed. `.env` is git-ignored;
`.env.example` is committed with placeholders. The development signing key in
`appsettings.Development.json` is committed on purpose so `dotnet run` works with no setup — it
is worthless and must never appear elsewhere.

The working tree once held `App Config Dev/Stg/Prod.txt`, carrying live credentials in plaintext.
Those files have been **deleted**, and the `App Config *.txt` pattern stays in `.gitignore` as a
second line of defence rather than as the fix. Ignoring a secret is not remediation: if any of
those values ever reached a commit, a synced folder, or a backup, **they must be rotated at the
source** — deleting the file does not un-share the credential. CI enforces the rule going forward
with a `Check for committed secrets` step that fails the run if a file matching those patterns is
ever tracked again.

**Log redaction.** A Serilog destructuring policy masks any property named `password`,
`confirmPassword`, `accessToken`, `refreshToken`, `tokenHash`, `signingKey`, and similar. Making
the safe outcome the default beats relying on every call site to remember.

---

## Deliberately not built

Stating what was left out, and why, is worth more than half-building it.

- **No `PATCH`.** JSON Patch on a six-field resource adds a dependency and a semantics
  discussion for no benefit.
- **No hard delete.** Soft delete only. If it is ever needed it is a separate admin endpoint with
  its own policy, not a query flag.
- **No email verification or password reset.** Both need a mail transport. First thing to add.
- **No `Money` value object.** One currency, and `Money` without a `Currency` is a `decimal`
  wearing a hat. The trigger to introduce it is a second currency.
- **No outbox or message bus.** Nothing consumes events yet.
- **No per-owner product authorization.** Any authenticated user may edit any product — the
  assessment describes a shared catalogue and defines no ownership model, and an unenforced
  ownership model is worse than none. The extension point is a resource-based
  `IAuthorizationHandler` comparing `CreatedBy` to `sub`.
- **No instant JWT revocation.** An access token stays valid until it expires (≤15 min); that is
  inherent to stateless JWT. The mitigation, if it is ever required, is a `jti` denylist in Redis.
- **No `AutoMapper`.** Six fields, hand-written, faster at runtime and debuggable with a
  breakpoint — and it avoids the v15+ licence.

## Open questions

1. Should `DELETE` be Admin-only, or should a `User` delete products they created? Admin-only was
   chosen because the assessment mentions role-based authorization but defines no ownership model.
2. Should `GET /products` require authentication? Specified public because browsing a catalogue
   is a public action; tightened by changing one policy in `AuthorizationExtensions`.
3. Is `If-Match` concurrency required, or is last-write-wins acceptable? Currently opt-in —
   sending the header gets you compare-and-swap, omitting it gets last-write-wins. Making it
   mandatory is a two-line change plus a `428` on the error table.
