# kart-offer-service

Coupon validation/redemption, pricing quotes, and promotion campaigns for the Kart e-commerce
platform — a merge of the BRD's Coupon, Pricing, and Promotion services per
`kart-platform` ADR-0001. Three aggregate roots (`Coupon`, `PricingQuote`, `PromotionCampaign`)
in one bounded context.

Built as a **CQRS, high-throughput service**: PostgreSQL is the single write-side source of
truth; MongoDB (sharded in production) is a denormalized, eventually-consistent read side kept in
sync via a transactional outbox → RabbitMQ → self-consumed projection pipeline. See
[Architectural deviation](#architectural-deviation-from-the-approved-design-docs) below for why
this differs from the platform's originally-approved database design.

## Tickets (OFF-1 .. OFF-10) — all implemented

| Ticket | Feature | Slice |
|---|---|---|
| OFF-1 | Validate coupon | `Application/Features/ValidateCoupon` |
| OFF-2 | Redeem coupon | `Application/Features/RedeemCoupon` |
| OFF-3 | Void coupon redemption on order cancellation | `Application/Features/VoidCouponRedemption` |
| OFF-4 | Ingest catalog price changes | `Application/Features/RecomputeCatalogPrice` |
| OFF-5 | Create/activate promotion campaign | `Application/Features/CreatePromotionCampaign` |
| OFF-6 | Deactivate promotion campaign | `Application/Features/DeactivatePromotionCampaign` |
| OFF-7 | Get active promotions | `Application/Features/GetActivePromotions` |
| OFF-8 | Get pricing quote | `Application/Features/GetPricingQuote` |
| OFF-9 | Issue coupon | `Application/Features/IssueCoupon` |
| OFF-10 | Deactivate coupon | `Application/Features/DeactivateCoupon` |

Two additional read slices (`GetCouponAdminView`, `GetPromotionCampaignAdminView`) round out
api-contract.yaml's admin surface — they exist so a caller can obtain the `version` an `If-Match`
deactivate call requires, per `kart-admin-service`'s optimistic-concurrency convention.

## Architecture

```
src/
├── Api/              Controllers, JWT admin auth, Program.cs composition root
├── Application/       MediatR commands/queries, one folder per vertical slice
├── Domain/            Coupon, PricingQuote, PromotionCampaign aggregates (no framework deps)
└── Infrastructure/
    ├── Persistence/           PostgreSQL (EF Core) - write side, source of truth
    ├── Persistence/ReadModel/ MongoDB - CQRS read side (denormalized, sharded)
    └── Messaging/             RabbitMQ - manifest-driven topology, outbox relay, consumers
```

- **Clean Architecture + Vertical Slice**, per `agent-reusables/docs/standards/folder-structure.md`.
- **Message bus**: `contracts/message-bus-manifest.json` is the single source of truth for the
  entire RabbitMQ topology (exchanges, queues, bindings, dead-letter/retry ladders) — nothing is
  hardcoded in C#, exactly mirroring `kart-identity-service`/`kart-category-service`/
  `kart-delivery-tracking-service`'s pattern.
- **CQRS sync**: every domain event is written to `offer_outbox_events` in the same PostgreSQL
  transaction as its aggregate (`OfferDbContext.SaveChangesAsync`). `OutboxRelayHostedService`
  publishes it to `offer.exchange`. This service's own
  `ReadModelProjectionConsumerHostedService` self-consumes those same events (a wildcard-bound
  internal queue) and projects the change into MongoDB's `coupons_read`/
  `promotion_campaigns_read` collections — the only path that ever writes to the read side.
- **Global exception handling / consistent response envelope**: `Kart.Shared.ErrorHandling`
  (`AddKartErrorHandling`/`UseKartErrorHandling`) for thrown exceptions; `Api/Common/ResultExtensions`
  translates a handler's `Result` failure into the exact same `ProblemDetails` (RFC 7807 +
  `errorCode`/`traceId`) envelope, so a rejection looks identical regardless of which path
  produced it.
- **Observability**: `Kart.Shared.Observability` (Serilog + OpenTelemetry, standard sampling
  tier — Offer is not an Order Saga participant), `/metrics` Prometheus scrape endpoint.
- **Concurrency control**: `Coupon`/`PromotionCampaign` writes use EF Core's optimistic
  `version` concurrency token (matching `database-design.md`'s `UPDATE ... WHERE version = $2`
  semantics) for admin deactivate; `RedeemCoupon`/`DeactivateCoupon` additionally take a
  per-`coupon_code` `SELECT ... FOR UPDATE` lock (design-decisions.md) since they race a
  cap/window check, not just a single field update.
- **Throughput-oriented design choices**: the highest-QPS checkout-path reads
  (`ValidateCoupon`, `GetActivePromotions`) are served from the sharded MongoDB read model, never
  PostgreSQL; money-critical writes (`RedeemCoupon`, admin issue/deactivate) and the
  self-contained `GetPricingQuote` computation stay on PostgreSQL for strong consistency.

## Architectural deviation from the approved design docs

`kart-platform`'s originally-approved `database-design.md` for this service specifies
PostgreSQL as the *only* store (Redis-cached reads, no MongoDB). The user explicitly requested a
different architecture for this build: **a separate read database (MongoDB, sharded) with CQRS
sync and denormalized read tables**, prioritizing very high read throughput (100k–1M req/s
class). This repository implements that explicit instruction rather than the platform docs'
original (pre-existing) design — Redis write-through caching was dropped in favor of the Mongo
read model to avoid running three storage systems for the same concern. Everything else
(message-bus-manifest pattern, DDD model, event contract, API contract, concurrency/locking
decisions) follows the approved docs exactly.

## Running locally

```bash
docker compose up -d postgres mongo rabbitmq
export PATH="$PATH:$HOME/.dotnet/tools"   # dotnet-ef, if not already installed
dotnet ef database update --project src/Infrastructure/KartOfferService.Infrastructure.csproj \
  --startup-project src/Infrastructure/KartOfferService.Infrastructure.csproj \
  --context KartOfferService.Infrastructure.Persistence.OfferDbContext
dotnet run --project src/Api/KartOfferService.Api.csproj
```

Or build/run the whole stack: `docker compose up --build`.

## Testing

```bash
dotnet test KartOfferService.sln
```

- `tests/UnitTests` — Domain aggregate/value-object tests + Application handler tests (mocked
  repositories via NSubstitute). **69 tests.**
- `tests/IntegrationTests` — real PostgreSQL/MongoDB/RabbitMQ via Testcontainers: outbox
  transactionality, optimistic-concurrency races, owned-type/JSONB round-trips, RabbitMQ topology
  declaration against the actual committed manifest, and the Mongo projection write/read path.
  **15 tests.**
- `tests/ContractTests` — `WebApplicationFactory`-driven HTTP wire tests against
  `contracts/api-contract.yaml` (status codes, JSON shape, RBAC gating), with in-memory fakes
  standing in for PostgreSQL/MongoDB/RabbitMQ. **27 tests.**

All 111 tests pass. The full CQRS pipeline (PostgreSQL write → outbox → RabbitMQ → MongoDB read
model) was also manually verified end-to-end against real containers: an issued/redeemed coupon
written to PostgreSQL under a row lock became visible via `POST /v1/coupons/validate` (which reads
only the MongoDB projection) within one outbox-relay poll interval, and a repeated
`Idempotency-Key`/order-id redeem attempt was correctly served as an idempotent no-op rather than
double-counted.

## Known scope simplifications (documented, not defects)

- `ProductPriceChanged` carries no currency field (event-contract.md) — materialized prices are
  assumed USD (`Application.Common.Constants.DefaultCurrency`); no multi-currency conversion.
- `promotion_campaigns` has no SKU association (matches `database-design.md`'s schema exactly) —
  `GetActivePromotions`'s optional `sku` query parameter is accepted but currently a no-op filter.
- `ValidateCoupon`'s per-user redemption cap is enforced authoritatively at redeem time only (the
  Mongo-backed validate read checks window + global cap); this is intentional — api-contract.yaml
  states validate "does not redeem," and redeem always re-checks under a PostgreSQL row lock
  regardless of what validate returned.
