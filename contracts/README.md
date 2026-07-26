# contracts/

- `api-contract.yaml` - vendored, read-only copy of the approved OpenAPI contract synced from
  `kart-shared/contracts/kart-offer-service/api-contract.yaml` (itself synced from `kart-platform`).
  Never hand-edited here.
- `event-contract.md` - vendored, read-only copy of the approved event contract, same sync path.
- `message-bus-manifest.json` - **this repo's own source of truth** for its RabbitMQ topology
  (exchanges, queues, bindings, dead-letter/retry wiring). Unlike the two files above, this one is
  owned and maintained in this repo, not synced - it completes the draft sketch that lives in
  `kart-platform/docs/services/kart-offer-service/message-bus-manifest.json` with this service's
  actual publishing topology and its internal read-model projection queue. Nothing RabbitMQ-related
  is hardcoded in C#; `RabbitMqTopologyProvisioner` declares everything straight from this file.
