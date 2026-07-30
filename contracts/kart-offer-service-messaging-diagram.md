# kart-offer-service — Messaging Flow Diagram

Generated from [`message-bus-manifest.json`](./message-bus-manifest.json). Covers the full lifecycle:
publish fan-out, consume bindings, ack/nack branching, dead-letter routing, retry ladder escalation
(dashed TTL-expiry requeue back to the origin queue), and terminal DLQ parking.

Each exchange is labeled on two independent axes:
- **ownership**: owned by this service vs. external (bind-only, never declared here)
- **role**: publish source, dead-letter exchange (DLX), or bind-only

```mermaid
flowchart TD
    SVC["kart-offer-service"]

    %% ===== Exchanges =====
    EXOFFER{{"offer.exchange<br/>owned · publish + bind-source"}}
    EXDLX{{"offer.dlx<br/>owned · dead-letter only"}}
    EXPRODUCT[/"product.exchange<br/>external · bind-only"/]
    EXORDER[/"order.exchange<br/>external · bind-only"/]

    %% ===== 1. Publish path: fan-out per published event =====
    SVC --> EXOFFER
    EXOFFER -->|"routingKey: offer.coupon.redeemed"| EVT_CR(["CouponRedeemed"])
    EXOFFER -->|"routingKey: offer.coupon.redemption-voided"| EVT_CRV(["CouponRedemptionVoided"])
    EXOFFER -->|"routingKey: offer.coupon.issued"| EVT_CI(["CouponIssued"])
    EXOFFER -->|"routingKey: offer.coupon.deactivated"| EVT_CD(["CouponDeactivated"])
    EXOFFER -->|"routingKey: offer.pricing.quote-issued"| EVT_PQI(["PriceQuoteIssued"])
    EXOFFER -->|"routingKey: offer.promotion.activated"| EVT_PA(["PromotionActivated"])
    EXOFFER -->|"routingKey: offer.promotion.deactivated"| EVT_PD(["PromotionDeactivated"])

    %% ===== 2. Consume path: bindings -> queues -> ack/nack =====
    EXPRODUCT -->|"routingKey: product.price.changed"| QPRODUCT["offer.product-events.queue"]
    EXORDER -->|"routingKey: order.order.cancelled"| QORDER["offer.order-events.queue"]
    EXOFFER -->|"routingKey: offer.coupon.#"| QPROJ["offer.read-model-projection.queue"]
    EXOFFER -->|"routingKey: offer.promotion.#"| QPROJ

    QPRODUCT -->|ack| OKPRODUCT(("processed"))
    QPRODUCT -->|nack| EXDLX
    QORDER -->|ack| OKORDER(("processed"))
    QORDER -->|nack| EXDLX
    QPROJ -->|ack| OKPROJ(("processed"))
    QPROJ -->|nack| EXDLX

    %% ===== 3. Dead-letter + retry ladder (ascending TTL) =====
    %% -- offer.product-events.queue ladder --
    EXDLX -->|"deadLetter routingKey: offer.product-events.dlq"| RPRODUCT30["offer.product-events.retry.30s<br/>ttl: 30000ms"]
    RPRODUCT30 --> RPRODUCT5M["offer.product-events.retry.5m<br/>ttl: 300000ms"]
    RPRODUCT30 -.->|"TTL expiry: requeue"| QPRODUCT
    RPRODUCT5M -.->|"TTL expiry: requeue"| QPRODUCT

    %% -- offer.order-events.queue ladder --
    EXDLX -->|"deadLetter routingKey: offer.order-events.dlq"| RORDER30["offer.order-events.retry.30s<br/>ttl: 30000ms"]
    RORDER30 --> RORDER5M["offer.order-events.retry.5m<br/>ttl: 300000ms"]
    RORDER30 -.->|"TTL expiry: requeue"| QORDER
    RORDER5M -.->|"TTL expiry: requeue"| QORDER

    %% -- offer.read-model-projection.queue ladder --
    EXDLX -->|"deadLetter routingKey: offer.read-model-projection.dlq"| RPROJ30["offer.read-model-projection.retry.30s<br/>ttl: 30000ms"]
    RPROJ30 --> RPROJ5M["offer.read-model-projection.retry.5m<br/>ttl: 300000ms"]
    RPROJ30 -.->|"TTL expiry: requeue"| QPROJ
    RPROJ5M -.->|"TTL expiry: requeue"| QPROJ

    %% ===== 4. Final tier exhausted -> terminal DLQ =====
    RPRODUCT5M --> DLQPRODUCT[["offer.product-events.dlq"]]
    RORDER5M --> DLQORDER[["offer.order-events.dlq"]]
    RPROJ5M --> DLQPROJ[["offer.read-model-projection.dlq"]]

    %% ===== Styling =====
    classDef ownedExchange fill:#2563eb,color:#fff,stroke:#1e3a8a,stroke-width:2px;
    classDef externalExchange fill:#ffffff,color:#374151,stroke:#6b7280,stroke-width:2px,stroke-dasharray: 5 5;
    classDef queue fill:#10b981,color:#fff,stroke:#065f46,stroke-width:2px;
    classDef retryTier fill:#f59e0b,color:#1f2937,stroke:#b45309,stroke-width:2px;
    classDef dlx fill:#7c3aed,color:#fff,stroke:#4c1d95,stroke-width:2px;
    classDef dlq fill:#dc2626,color:#fff,stroke:#7f1d1d,stroke-width:2px;

    class EXOFFER ownedExchange
    class EXDLX dlx
    class EXPRODUCT,EXORDER externalExchange
    class QPRODUCT,QORDER,QPROJ queue
    class RPRODUCT30,RPRODUCT5M,RORDER30,RORDER5M,RPROJ30,RPROJ5M retryTier
    class DLQPRODUCT,DLQORDER,DLQPROJ dlq
```

## Notes

- **`offer.exchange`** is owned and serves two roles at once: it is the publish target for all
  seven domain events emitted via the transactional outbox, *and* a bind-source — this service
  self-consumes its own `offer.coupon.#` / `offer.promotion.#` events on
  `offer.read-model-projection.queue` to keep its MongoDB CQRS read models in sync.
- **`offer.dlx`** is owned but is dead-letter-only — it never carries a published domain event,
  it only receives nacked messages from the three consumer queues and routes them into their
  respective retry ladders / terminal DLQs.
- **`product.exchange`** and **`order.exchange`** are external — bind-only, never declared by this
  service.
- Published domain events (`CouponRedeemed`, `CouponRedemptionVoided`, `CouponIssued`,
  `CouponDeactivated`, `PriceQuoteIssued`, `PromotionActivated`, `PromotionDeactivated`) have no DLQ
  of their own: the outbox relay retries indefinitely until RabbitMQ is reachable rather than
  dead-lettering.
- All three retry ladders share the same two-tier shape (30s, then 5m) and the same `requeueTo`
  target (the originating queue itself); after the 5m tier is exhausted the message parks in that
  queue's terminal DLQ.
