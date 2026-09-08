# NeverOrder — project status

**Last updated:** 7 September 2026
**Phases complete:** 1, 2, 3, 4 of 6
**Build:** clean (0 warnings, `TreatWarningsAsErrors=true`) · **Tests:** 83 passing · **Lint:** clean

---

## Contents

- [Where the project stands](#where-the-project-stands)
- [What is built](#what-is-built)
- [Verified behaviour](#verified-behaviour)
- [How to run it](#how-to-run-it)
- [Known gaps and limitations](#known-gaps-and-limitations)
- [What is left to do](#what-is-left-to-do)
- [Technical debt](#technical-debt)
- [Decision log](#decision-log)

---

## Where the project stands

NeverOrder is a virtual commerce platform: browse, cart, order and track, with no payment and no
fulfilment anywhere in the system. It exists to demonstrate production-shaped backend engineering.

The end-to-end journey works today — register, browse, add to cart, check out, and watch an order move
`Created → Confirmed → Preparing → OutForDelivery → Delivered` with updates pushed over a WebSocket.

| Phase | Scope | Status |
| --- | --- | --- |
| 1 | Domain, persistence, auth, catalogue, cart, checkout | Complete |
| 2 | Outbox, idempotent consumers, retry ladder, dead letters | Complete |
| 3 | Caching, optimistic concurrency, health, logging, rate limiting | Complete |
| 4 | SignalR real-time order tracking | Complete |
| 5 | Admin and dashboard | Not started |
| 6 | Service-backed integration tests | Not started |

---

## What is built

### Phase 1 — the core journey

- Registration and login on ASP.NET Core Identity, issuing JWT bearer tokens.
- Product catalogue with search, category filter, price range, stock filter, sorting and pagination.
- Per-user cart with server-side validation and totals.
- Checkout that re-reads every line from the live catalogue and recomputes the total server-side.
- Order history, order detail and a tracking timeline.
- React 19 + TypeScript frontend covering the whole journey.

Clean-architecture dependency rule, enforced purely by project references:

```
Domain  ←  Application  ←  Infrastructure  ←  Api
```

`NeverOrder.Domain` has zero NuGet dependencies. It holds entities, the order state machine and event
contracts, and knows nothing about EF Core, ASP.NET or RabbitMQ.

### Phase 2 — reliable messaging

- **Transactional outbox** — events are written in the same transaction as the state change that caused
  them, so a broker outage cannot leave a committed order without its event.
- **Idempotent consumers** — a composite `ProcessedEvent` primary key makes duplicate delivery a no-op.
  The guard row is inserted before the handler runs and committed by the same `SaveChanges`.
- **Bounded retries** — 1s, 2s, 4s, 8s using broker TTL queues, so backoff survives a consumer crash and
  costs no threads. No plugin required.
- **Dead-letter queue** — failure reason, attempt count and payload persisted for inspection.
- Every background worker backs off when a dependency is down instead of flooding the logs.

### Phase 3 — performance and reliability

- **Read-through catalogue cache with version-key invalidation.** Every cache key embeds a version
  token; invalidating publishes one new token and orphans the whole previous generation at once. Runs on
  Redis when configured and in-process otherwise, through the same `IDistributedCache` code path.
- **`xmin` optimistic concurrency on orders.** PostgreSQL already stamps each row with the transaction
  that last wrote it, so this costs no schema. A second instance's `UPDATE ... WHERE xmin = @original`
  matches zero rows and is skipped.
- **Health checks.** `/health/live` reports only that the process is running; `/health/ready` covers each
  dependency, and each dependency registers its own probe next to its own DI registration.
- **Serilog with correlation ids.** The id is read from `X-Correlation-Id` or generated, echoed in the
  response, and written into the event envelope stored in the outbox, so a consumer's logs point back to
  the originating request.
- **Rate limiting.** A global policy partitioned by account when signed in and by address when not, plus
  a tighter address-keyed policy on register and login.

### Phase 4 — real time

- **SignalR hub** at `/hubs/orders`, one group per user (`user:{sub}`).
- The hub exposes **no subscribe method**. Membership is derived from the token on connect, so there is
  no order id for a client to tamper with.
- Notifications are raised by the component that owns the transition, **after** the commit.
- The tracking page is pushed to, and **falls back to polling** if the hub cannot connect.
- Optional Redis backplane, wired from the same `Cache:Redis` connection string.

### Frontend

- Light commerce theme: white surfaces, teal accent (`#0f766e`), soft shadows, square product tiles.
- Product imagery is generated locally as deterministic SVG, so the catalogue paints with **zero image
  network requests** and works offline.
- `useOrderStream` reports whether the hub is genuinely connected, and the page keeps its polling timer
  until it is.

---

## Verified behaviour

Everything below was measured on a running instance, not inferred.

| Claim | Evidence |
| --- | --- |
| Catalogue cache serves without touching the database | Two identical queries executed the products SQL **once** |
| `xmin` token is live | Generated SQL: `WHERE "Id" = @p9 AND xmin = @p10 RETURNING xmin` |
| Readiness fails but liveness holds | Postgres stopped → `/health/ready` **503** naming `postgres`, `/health/live` **200** |
| Correlation id threads through logs | Request logs carry `[my-trace-123]`; worker logs carry `[]` |
| Auth rate limit engages | 14 rapid logins → `401×10` then `429`, `Retry-After: 60`, ProblemDetails body |
| Live updates arrive with no polling | 4 pushes: `Confirmed → Preparing → OutForDelivery → Delivered` |
| A user cannot see another user's orders | Second connected user received **0** pushes |
| Anonymous cannot open the hub | `/hubs/orders/negotiate` → **401** |
| Catalogue is fast | `/api/products` **1.4–3.4 ms** server-side; page `domComplete` **411 ms**, 0 image requests |

### Test suites

| Suite | Count | Needs services? |
| --- | --- | --- |
| `NeverOrder.Tests.Unit` | 69 | No |
| `NeverOrder.Tests.Integration` | 14 | No |

The unit suite covers every legal **and illegal** order transition, order and cart money arithmetic and
the simulation schedule. The second suite covers persistence mapping, cache version invalidation and
per-user notification routing. Both run on a bare checkout — `dotnet test` needs no database, broker or
cache.

---

## How to run it

### With Docker

```bash
cp .env.example .env
docker compose up -d          # PostgreSQL, Redis, RabbitMQ
dotnet run --project backend/NeverOrder.Api
cd frontend && npm install && npm run dev
```

### Without Docker

Only PostgreSQL is a hard requirement. Redis is optional, and RabbitMQ can be switched off.

```powershell
./scripts/pg-local.ps1                                                   # downloads + starts a repo-local PostgreSQL
dotnet user-secrets set "RabbitMq:Enabled" "false" --project backend/NeverOrder.Api
dotnet run --project backend/NeverOrder.Api                              # http://localhost:5038
cd frontend; npm install; npm run dev                                    # http://localhost:5173
```

`scripts/pg-local.ps1` downloads the official EnterpriseDB binaries into `.postgres/`, initialises a
cluster and starts it on loopback. No administrator rights, no installer, no Windows service.
`-Action Status` and `-Action Stop` manage it afterwards.

With the broker off, events are logged instead of published and the progression worker takes over
confirmation, so the full journey still works — the outbox, retry ladder and dead-letter path are simply
not exercised.

### Configuration worth knowing

| Setting | Default | Effect |
| --- | --- | --- |
| `RabbitMq:Enabled` | `true` | `false` routes events to a logger and skips the consumers |
| `Cache:Enabled` | `true` | `false` bypasses caching entirely |
| `Cache:Redis` | empty | Empty uses an in-process cache and no SignalR backplane |
| `Jwt:SigningKey` | empty | Generated per-run in Development; a hard startup failure elsewhere |
| `Seed:AdminPassword` | empty | Unset simply skips creating the admin account |

---

## Known gaps and limitations

1. **The Redis backplane is wired but only exercised single-instance.** Verifying multi-instance fan-out
   needs Redis, which needs Docker, which is unavailable on the current machine.
2. **No service-backed integration tests.** Nothing yet drives the API against a real PostgreSQL or
   RabbitMQ. That is Phase 6 and is why the EF mapping bug below survived so long.
3. **The in-process cache stops being a shared cache with more than one instance.** Correct for a single
   instance; configure Redis before scaling out.
4. **Product images are generated placeholders**, not photography.
5. **No refresh tokens.** Access tokens last 60 minutes and the user must sign in again.
6. **The admin account has no interface.** It is seeded with a role but Phase 5 has not been built.
7. **Stock is never decremented.** Checkout validates availability but nothing reserves it, so it is not
   yet a real inventory model.

---

## What is left to do

### Phase 5 — admin and dashboard

- [ ] Admin-only route group guarded by the `Admin` role.
- [ ] Catalogue management: create, edit, deactivate products and categories.
- [ ] Call `ICacheService.InvalidateVersionAsync(CacheKeys.CatalogVersion)` on every catalogue write —
      the seam already exists and is currently only called by the seeder.
- [ ] Dead-letter inspection: list `DeadLetterEvents` with failure reason, attempt count and payload.
- [ ] Dead-letter replay: republish a stored payload back onto the main exchange.
- [ ] Virtual spending statistics: orders per day, total virtual spend, most-ordered products.
- [ ] Admin UI pages in the frontend.

Worth doing alongside: `Product` will need an `xmin` concurrency token once admins can edit it
concurrently, and product writes are the first real reason for one.

### Phase 6 — integration tests

- [ ] Testcontainers fixtures for PostgreSQL and RabbitMQ.
- [ ] API tests over `WebApplicationFactory` against a real database, covering the full journey.
- [ ] Migration test: apply every migration to an empty database.
- [ ] Messaging drill: force a consumer failure and assert the message walks the retry ladder
      (1s → 2s → 4s → 8s) and lands in the dead-letter queue with its reason recorded.
- [ ] Idempotency test: deliver the same event twice and assert one state change.
- [ ] Concurrency test: two writers advancing one order, asserting exactly one wins.
- [ ] Wire `dotnet test` in CI so both suites run on every push.

### Cross-cutting, not tied to a phase

- [ ] CI pipeline: build, test, lint, and fail on warnings.
- [ ] Refresh tokens and token revocation.
- [ ] OpenTelemetry traces to complement the correlation ids.
- [ ] Frontend tests — there are none at all today.
- [ ] Accessibility pass on the new theme (focus order, contrast, keyboard traps).
- [ ] Container image and a deployment target.

---

## Technical debt

| Item | Why it matters | Effort |
| --- | --- | --- |
| No frontend tests | The cart bug below was a UI bug, found by eye, not by a test | Medium |
| No CI | Nothing enforces the clean build and passing tests | Small |
| `OrderStatusChanged` is consumed but unused | The consumer logs it and stops; real-time now comes from the transition instead | Small |
| Seeded `imageUrl` is dead data | Every row points at an external service the frontend deliberately ignores | Small |
| Stock is never reserved | Two users can both check out the last item | Medium |

### Bugs found and fixed during this work

- **Domain-assigned `Guid` keys were issuing `UPDATE` instead of `INSERT`.** EF treats `Guid` keys as
  store-generated by convention, so a child reached through a tracked parent's navigation looked like an
  existing row. `POST /api/cart/items` returned 500 and `Order.TransitionTo()` had the same latent flaw.
  Fixed as one model-wide rule so a newly added entity cannot reintroduce it.
- **Missing `UserSecretsId`** meant the README's own `dotnet user-secrets` instructions failed outright.
- **A shared React Query mutation greyed out every "Add to cart" button** instead of the one clicked.
- **`aspect-ratio` was silently ignored** because the images carried `width`/`height` attributes without
  `height: auto`, rendering 266×600 tiles.
- **SignalR logged a negotiation error on every mount** because StrictMode's cleanup aborted the
  handshake mid-flight.
- **A lint failure blocked a clean `npm run lint`** — `AuthContext.tsx` exported both a component and a
  hook, defeating Fast Refresh.

---

## Decision log

Short version of the reasoning captured in the README's *Engineering decisions* section.

| Decision | Rationale |
| --- | --- |
| Scheduler queue lives in the database | `Order.NextTransitionAt` means a restart mid-order resumes instead of stranding it |
| Broker owns delivery, scheduler owns timing | Both components do real work and neither can advance an order twice |
| Money never comes from the client | Checkout re-reads the catalogue and recomputes totals server-side |
| One `SaveChanges`, one transaction | A failure cannot leave a half-placed order |
| Another user's order is a 404, not a 403 | Queries filter by the `sub` claim inside the predicate |
| Enums stored as text | `Preparing`, not `2`, keeps the database readable during demos |
| Retries use TTL queues | Backoff survives a consumer crash and costs no memory or threads |
| Idempotency enforced by a primary key | A racing duplicate loses the insert rather than being checked for |
| Aggregate ids are domain-assigned, and the mapping says so | Otherwise EF updates rows that do not exist |
| Carts have no concurrency token | Two tabs adding different products do not conflict; a token would cause spurious 409s |
| Cache invalidation publishes a version | One write invalidates a whole generation; deleting keys is what usually goes wrong |
| Liveness checks nothing | Otherwise one database outage restarts every healthy instance at once |
| Rate limits keyed by account where possible | Keying only by IP throttles everyone behind one NAT as a single client |
| Live updates hang off the transition | Announcing from the consumer would do nothing when the broker is disabled |
| Clients cannot ask to watch an order | A client that can name an order can name someone else's |

---

## A reminder

NeverOrder performs **no real transactions**. There is no payment provider, no merchant, no delivery and
no money. Every price, order and delivery in this application is simulated.
