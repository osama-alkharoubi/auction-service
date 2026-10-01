# Auction Service

A multi-tenant auction service built with ASP.NET Core and PostgreSQL. Each organization runs auctions on items, and bidders place bids under real concurrency and business rules. The system does not process payments.

## Tech Stack

.NET 8 / ASP.NET Core · C# · PostgreSQL 16 · EF Core 8 (write side) · Dapper (read side) · Docker Compose · xUnit · FluentAssertions · JWT

## Architecture

```plaintext
Api              HTTP layer, authentication, middleware, filters
Application      Business logic, services, DTOs, interfaces
Domain           Entities, enums, domain rules
Infrastructure   EF Core, PostgreSQL, Dapper read queries, repositories, migrations, outbox processing
IntegrationTests API-level integration and concurrency tests
```

The write side uses EF Core and repositories. Read-heavy queries use Dapper only.

---

## How to Run

**Prerequisites:** .NET 8 SDK, Docker Desktop, Git.

```sh
git clone <repository-url>
cd AssessmentStarter
docker compose up -d                 # PostgreSQL 16
dotnet restore
dotnet ef database update --project Infrastructure --startup-project Api
dotnet run --project Api
dotnet test                          # integration tests
```

> Migrations are applied by `dotnet ef database update`.

Swagger: <https://localhost:7176/swagger/index.html>

### Quick try (about 2 minutes)

Seeded data: 1 organization, 1 seller, 2 bidders, 1 `Live` auction with no bids.

| Item | Value |
|---|---|
| Organization Id | `11111111-1111-1111-1111-111111111111` |
| Seller (`22222222-...`) | `seller@auction.test` / `Password1234!` |
| Bidder 1 (`33333333-...`) | `bidder@auction.test` / `Password1234!` |
| Bidder 2 (`55555555-...`) | `bidder2@auction.test` / `Password1234!` |
| Live auction Id | `44444444-4444-4444-4444-444444444444` |

All seeded users share the demo password `Password1234!` (local development only).

The seed is idempotent: running it again does not create duplicates. The demo auction is `Live` (started 1 hour ago, ends in 7 days) with starting price 100.00 and a minimum increment of 5.00.

```sh
# -k accepts the local development HTTPS certificate
# 1. Log in (returns a JWT)
curl -k -X POST https://localhost:7176/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"bidder@auction.test","password":"Password1234!"}'

# 2. Place a bid
curl -k -X POST https://localhost:7176/api/auctions/44444444-4444-4444-4444-444444444444/bids \
  -H "Authorization: Bearer <JWT>" \
  -H "X-Org-Id: 11111111-1111-1111-1111-111111111111" \
  -H "Idempotency-Key: demo-bid-1" \
  -H "Content-Type: application/json" \
  -d '{"amount": 150.00}'
```

---

## Triage: the three most important decisions

* **Concurrency:** bid placement takes a PostgreSQL pessimistic row lock (`SELECT ... FOR UPDATE`) on the auction inside the bid transaction, so concurrent bids on one auction are evaluated one at a time. Full reasoning in the [ADR](#adr-001-concurrency-strategy-for-bid-placement).
* **Multi-tenancy:** the tenant is resolved from the authenticated request by an action filter into a scoped `ITenantContext`. The `X-Org-Id` header must match the organization in the JWT, otherwise `403`.
* **Read-side authorization:** listing and leaderboard use one parameterized Dapper SQL path where `@Role` and `@UserId` drive column masking, not separate queries per role.

---

## Authentication and Multi-Tenancy

* JWT claims: user id, organization id, role (`Admin`, `Seller`, `Bidder`).
* Every request also sends `X-Org-Id: <organization-id>`.
* A mismatch between the header and the JWT organization returns `403 Forbidden` with a **generic body** (no hint about which organization exists).
* Looking up another tenant's resource by id returns `404 Not Found`, so existence is not leaked.
* An `IAsyncActionFilter` resolves a scoped `ITenantContext`. Controllers and repositories never read the header.
* **Write side:** EF Core global query filters enforce tenant isolation and soft-delete.
* **Locking query:** the `FOR UPDATE` auction query runs through EF Core, so it relies on the global query filters (tenant and soft-delete) rather than adding its own `org_id` predicate.
* **Read side:** EF global filters do **not** apply to Dapper, so the Dapper queries filter by organization explicitly through the `@OrgId` parameter.

---

## Auction State Machine

```plaintext
Draft -> Scheduled -> Live -> Closed -> Settled
          any state -> Cancelled (Admin only, reason required)
```

| Transition | Rule |
|---|---|
| Draft -> Scheduled | allowed |
| Scheduled -> Live | only when `StartsUtc` has been reached |
| Live -> Closed | only when `EndsUtc` has been reached, or by admin cancellation |
| Closed -> Settled | allowed |
| any -> Cancelled | Admin only, with a reason |

Invalid transitions return `409 Conflict` naming the attempted source and destination states. Rules live in an explicit transition table outside the controller (no `if/else` chain).

> **Note:** time-based transitions (`Scheduled -> Live`, `Live -> Closed`) are triggered manually via `PATCH /api/auctions/{id}/state`. Automatic scheduling (Hangfire) is a stretch goal that is not implemented.

---

## Bid Placement

`POST /api/auctions/{id}/bids` requires an `Idempotency-Key` header.

A bid is accepted only when:

* the auction is `Live` and `StartsUtc <= now < EndsUtc`
* `amount >= currentHighBid + MinIncrement`
* the bidder is not the seller
* the bidder is not outbidding their own currently winning bid

**Accepted bid:** in one transaction: insert the bid as `Winning`, mark the previous winner `Outbid`, update `Auction.CurrentHighBidId`, append an `AuctionAuditLog` row, append a `BidAccepted` outbox message.

**Rejected bid:** the `Rejected` bid (with `RejectReason`) and its audit row are **committed**, not rolled back, so rejections stay auditable. Rejected responses are stored in the idempotency record too, so a replay returns the same rejection.

---

## ADR-001: Concurrency strategy for bid placement

**Status:** Accepted

### Context

Many bidders can bid on the same auction at the same moment. The rules depend on the current high bid, so two requests that read the same state must not both win. The requirement is exactly one `Winning` bid after 50 parallel bids, never two, and never zero unless every bid was invalid.

### Decision

**PostgreSQL pessimistic locking.** Inside the bid transaction the auction row is loaded with:

```sql
SELECT ... FROM auctions
WHERE id = {0}
FOR UPDATE;
```

The query is executed through EF Core (`FromSql`), so the tenant and soft-delete global query filters still apply to it.

Concurrent bids on the same auction queue on that row lock and are evaluated one at a time. Each request reads the latest state after it gets the lock, so a bid that became too low because a higher one was just accepted is rejected against the updated state.

```plaintext
Bid A --+
Bid B --+
Bid C --+--> auction row lock --> evaluated one at a time
Bid D --+
Bid E --+
```

### Why not optimistic concurrency (RowVersion) first?

I **started with RowVersion**, because the entity structure already had a `RowVersion` concurrency token on `BaseEntity`, so it was the cheapest option to build. I then chose `FOR UPDATE` for one reason: with optimistic concurrency, under contention most requests fail with `DbUpdateConcurrencyException`. There are only two ways to handle that, and I did not want either:

* **Return the error to the bidder (409).** Valid bids would fail only because someone else bid a few milliseconds earlier. The bidder would have to resend.
* **Retry in a loop.** With N concurrent bids on one auction, each round only one request succeeds, so the rest keep retrying. Work grows roughly with N squared, there is no clean upper bound on retries, and a request can still run out of retries and fail.

The requirement is that every bid gets properly evaluated. With a lock, requests **wait in a queue instead of failing**, each is processed exactly once, and each gets a real accept or reject result based on current state, not a technical conflict error.

### Alternatives considered

| Option | Why not chosen |
|---|---|
| Optimistic retry (`RowVersion`) | Conflict errors or unbounded retry loops under contention (see above). |
| Postgres advisory lock | Works, but locks a number or hash instead of the row. It is easier to misuse and does not tie to the row we are updating. |
| Single-statement conditional upsert | Hard to express all the rules (increment, own-bid, seller, window) plus audit and outbox rows in one statement. |

### Consequences

* Bids on the **same auction** are serialized; bids on **different auctions** never block each other (the lock is per row).
* The lock is held only for the length of one short transaction, so waiting time stays small.
* **Trade-offs accepted:**
  * Throughput on a single hot auction is limited by transaction time, because its bids run one after another.
  * Waiting requests each hold a pooled database connection, so a large burst on one auction can put pressure on the connection pool.
  * The transaction must stay short: no external calls inside it, only the database work.
  * Deadlocks are not expected, because each bid transaction locks a single auction row.
  * No `lock_timeout` is set yet, so a stalled transaction would make waiters queue behind it. See [Next steps](#scope).
* The `RowVersion` token stays enabled as a secondary guard for ordinary EF updates (see [EF Core Write Side](#ef-core-write-side)). `FOR UPDATE` is the primary mechanism for the bid race. Optimistic conflicts elsewhere surface as `409`.
* **The idempotency re-check runs after the lock is acquired** (see below).

### When would the alternatives pay off?

* **Postgres advisory locks:** when you need to serialize something that is not a single row (e.g. a per-user or per-resource-group workflow) or lock before the row exists.
* **SQL Server `sp_getapplock`:** the same idea on SQL Server, which has no `pg_advisory_lock`. It is useful for application-level critical sections tied to a transaction or session.
* **Redis Redlock:** when the contended resource spans multiple databases or services, so no single database lock can cover it. It adds failure modes (clock drift, lock expiry) and should not replace a database lock when one database already owns the data.

---

## Idempotency

* `Idempotency-Key` is required. Scope is **`(UserId, Key)`**, matching the composite primary key of `idempotency_records`.
* The service does a fast lookup first. After the transaction starts **and the auction row lock is acquired**, it checks again before creating the bid. Checking only before the lock would let two concurrent same-key requests both pass.
* A replay returns the original status code, body and bid response. No second bid is created.
* The unique index on bids is `(bidder_user_id, idempotency_key)`, so two different users can use the same key string without a conflict.

---

## Outbox Pattern

Domain events are written to `outbox_messages` in the same transaction as the state change, so there is no dual write to an external broker.

```plaintext
DB transaction
 |- state change
 |- audit record
 '- outbox message
```

**Events:** `AuctionPublished`, `BidAccepted`, `AuctionClosed`, `AuctionSettled`.

**Restart safety (at-least-once, no loss):**

1. A `BackgroundService` polls unprocessed rows (`processed_utc IS NULL`), ordered by `occurred_utc`, with `FOR UPDATE SKIP LOCKED` in a transaction.
2. Each message is dispatched to the sink.
3. `processed_utc` is set only after successful dispatch; `attempts` is incremented on failure.
4. If the process dies mid-publish, the row is still unprocessed and is retried on restart, so **no event is lost**.

Because delivery is at-least-once, a message can be delivered twice in a crash window. Every event has a **stable Id** that consumers use to deduplicate, which gives effectively-once processing. The required sink is in-process (logger); RabbitMQ is a stretch goal.

---

## EF Core Write Side

* Generic `IRepository<T> where T : BaseEntity`. `BaseEntity` has `CreatedUtc`, `AvailabilityId`, `RowVersion`.
* **Optimistic concurrency token:** PostgreSQL has no automatic `rowversion` column like SQL Server, so `[Timestamp]` on a `byte[]` is not generated by the database. Instead `RowVersion` is an application-managed concurrency token: a new GUID-based token is assigned on every update, and EF checks the original token in the `UPDATE`. If it no longer matches, `DbUpdateConcurrencyException` is thrown.
* Soft delete: `AvailabilityId = Deleted` plus a global query filter.
* `IQueryable` never leaves the repository layer.
* `DbUpdateConcurrencyException` is translated to `409 Conflict` with a descriptive body.

---

## Dapper Read Side

Both endpoints are Dapper only, **one round trip each**, parameterized (no string-built `WHERE`), with no N+1.

* `GET /api/auctions?state=&endsBefore=&search=&cursor=&pageSize=`: keyset pagination (not offset).
* `GET /api/auctions/{id}/leaderboard?top=10`

`TimeRemainingSeconds` is computed in SQL.

### Localization

`AuctionState` and `BidState` are enums backed by `_Tr` translation tables (`en`, `ar`). State names are resolved **in SQL** from `Accept-Language`, falling back to English. No C# post-processing.

### Row-level and role-based authorization (one SQL path)

One parameterized query. `@Role` and `@UserId` drive `CASE WHEN` expressions, not three controller branches:

| Role | Bid amount | Bidder email |
|---|---|---|
| **Bidder** | own bids only; others are `NULL` | own only; others are `NULL` |
| **Seller** | all bids on auctions they own; `NULL` on other sellers' auctions | same as amount |
| **Admin** | everything inside their organization | same as amount |

```sql
-- illustrative shape
CASE WHEN @Role = 'Admin'
       OR (@Role = 'Seller' AND a.seller_user_id = @UserId)
       OR b.bidder_user_id = @UserId
     THEN b.amount END AS amount
```

---

## Audit Trail

`AuctionAuditLog` is append-only. It records state transitions, accepted bids and rejected bids, with `ActorUserId`, `Reason`, `BeforeStateId`, `AfterStateId` and JSON metadata, written in the same transaction as the operation.

---

## Rate Limiting

Bid placement: **20 bids per minute per `(OrgId, UserId)`**. Exceeding it returns `429 Too Many Requests` with `Retry-After`.

* Type: **fixed window** (1 minute, `PermitLimit = 20`, no queue), using the built-in ASP.NET Core `AddRateLimiter` with the named policy `BidsPerTenantPolicy`.
* Partition key: `{X-Org-Id}:{userId}`, where `userId` comes from the JWT `NameIdentifier` claim.
* A rejected request gets `429` and a `Retry-After` header (seconds) taken from the limiter's lease metadata.
* Known limit of a fixed window: a bidder can send up to 40 bids around a window boundary (20 at the end of one window, 20 at the start of the next). A sliding window would smooth this.
* State is **in-memory per instance**. Running multiple instances would need a shared store (e.g. Redis) to keep the limit global.

---

## Cross-cutting

* **Validation:** all inputs are validated; invalid requests return meaningful `400` responses.
* **Errors:** a single exception middleware maps exceptions to responses; stack traces are never returned.

---

## Database Migrations and Seed Data

EF Core migrations create the full schema from an empty database (tables, relationships, constraints, indexes, translation data).

Seed: 1 organization, 1 seller, 2 bidders, 1 live auction, 0 bids.

---

## Index Justifications

Key indexes:

| Index | Definition | Why |
|---|---|---|
| `IX_Auctions_Listing_Pagination` | `auctions (org_id, availability_id, created_utc DESC, id DESC)` | Tenant filter plus keyset cursor order for `GET /auctions`, with no sort step. |
| `IX_Bids_Leaderboard_Amount` | `bids (auction_id, availability_id, amount DESC, placed_utc ASC)` | Leaderboard reads the top N for one auction in index order. |
| `ix_outbox_messages_occurred_utc` (partial) | `outbox_messages (occurred_utc) WHERE processed_utc IS NULL` | The poller only scans unprocessed rows, and the index stays small as processed rows pile up. |
| `ix_bids_idempotency` (unique) | `bids (bidder_user_id, idempotency_key)` | Database-level guarantee of no duplicate bid per user and key. |
| `pk_idempotency_records` (PK) | `(idempotency_key, user_id)` | Per-user uniqueness and direct replay lookup. |
| `ix_auctions_current_high_bid_id` (unique) | `auctions (current_high_bid_id)` | One-to-one relation to the current high bid. |
| `ix_users_email` (unique) | `users (email)` | Unique login and lookup by email. |

Foreign-key support indexes: `ix_auctions_seller_user_id`, `ix_auction_audit_logs_actor_user_id`, `ix_auction_audit_logs_auction_id_id` (audit history in order), `ix_bids_bidder_user_id_placed_utc` (bidder history), `ix_users_org_id`.

**Dropped indexes:** `ix_auctions_org_id_state_id_ends_utc_id` and `ix_bids_auction_id_amount_placed_utc` were removed in migration `20261001131107_AddIdempotencyAndUpdates`. They were replaced by the two indexes above, which also include `availability_id` and match the keyset and leaderboard ordering actually used by the queries.

**Known limits:** the listing index covers tenant filtering and keyset order, not every optional filter. The `ILIKE` search has no trigram or full-text index; `pg_trgm` would be the next step.

DDL lives in `Infrastructure/Migrations` (`20260930131010_InitialCreate`, `20261001131107_AddIdempotencyAndUpdates`, `20261001165227_AddUserIdToIdempotencyRecord`).

---

## Testing

xUnit with `WebApplicationFactory`, using real HTTP requests.

| Scenario | Expectation |
|---|---|
| Concurrency | 50 parallel `POST /bids` on one auction end with exactly one `Winning` bid. |
| Idempotency | Same key replayed returns an identical response, no duplicate row. |
| State machine | 4+ invalid transitions return `409`. |
| Cross-tenant | A token from Org A cannot read Org B data. |
| Row-level auth | A bidder cannot see another bidder's email or amount in the leaderboard. |

> **Test data:** there are no CRUD endpoints, so the integration tests create what they need (extra bidders, a second organization, auctions in different states) directly through the DbContext, then exercise the system through real HTTP requests.

> **Concurrency test setup:** the rate limit is 20 bids/min per user and a bidder cannot outbid their own winning bid, so the test creates **50 distinct bidders** and sends one bid from each, so no user hits the limit and no bid is rejected for being a self-outbid. The assertion is exactly one `Winning` bid at the end.

---

## API

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/auctions/{id}/bids` | Place a bid |
| `GET` | `/api/auctions` | List auctions |
| `GET` | `/api/auctions/{id}/leaderboard` | Leaderboard |
| `PATCH` | `/api/auctions/{id}/state` | Change auction state |

Login endpoints are in the auth controller. Full contract: Swagger.

---

## Scope

**Implemented:** multi-tenancy, JWT, state machine, concurrent bidding, idempotency, transactional outbox, audit log, EF repositories, Dapper read models, SQL localization, role-aware data access, rate limiting, integration tests.

**Scope decision: no CRUD endpoints.** I did not build create/update/delete endpoints for organizations, users or auctions. The environment is populated by seed data (1 organization, 1 seller, 2 bidders, 1 live auction), and the goal of this task is to demonstrate the hard parts: concurrency, idempotency, multi-tenancy, the state machine, the outbox and row-level authorization. Routine CRUD would take time without showing anything beyond those. The repository layer (`IRepository<T>`) is generic, so adding CRUD later is straightforward.

**Not implemented (stretch goals):**

* Hangfire for automatic `Scheduled -> Live` and `Live -> Closed`
* RabbitMQ outbox sink with consumer dedup
* SignalR live leaderboard
* Materialized leaderboard read model kept in sync via the outbox

**Next steps if the project grows:**

1. **`lock_timeout` on the bid transaction** (`SET LOCAL lock_timeout = '3s'`). Today a stalled transaction would make waiting bids queue behind it and hold pooled connections. With a timeout, waiters fail fast (`PostgresException` `55P03`) and the API returns `503` with `Retry-After`.
2. Hangfire for automatic `Scheduled -> Live` and `Live -> Closed` transitions (removes the manual `PATCH` calls).
3. RabbitMQ as the real outbox sink, with consumer deduplication.
4. A trigram (`pg_trgm`) index for the `ILIKE` auction search.
5. Build the rate-limit partition key from the JWT organization claim instead of the raw `X-Org-Id` header. The limiter runs before the tenant filter validates the header, so unauthenticated requests with random header values would create many partitions.
6. CRUD endpoints for organizations, users and auctions.
