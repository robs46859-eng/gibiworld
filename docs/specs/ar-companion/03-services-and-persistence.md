# Services and persistence — GW-ARCH-003 v1.0.0

Parent: [architecture](../../GW-ARCH-003-AR-Companion-Build-Specification.md). Existing OpenAPI and migrations are contracts, not proof of running services. Proposed route/schema additions below must be implemented in `contracts/` before client generation; this specification does not change the current live contract.

## 1. Service boundary

Keep GW-ARCH-001's named TypeScript services and provider adapters. For this release, implement the smallest necessary behavior in `gw-edge-api`, `gw-game-service`, `gw-asset-service`, the isolated `gw-asset-worker`, and consent-aware `gw-telemetry`. Retain world/session/moderation/AI service boundaries, but shared-room and course endpoints are not prerequisites for solo fetch. Do not claim these folders exist before adding them.

| Component | Companion scope |
|---|---|
| Edge API | Token validation, account/region capability gate, authorization context, rate/body limits and request IDs |
| Game service | Pet ownership/read model, dwelling selection, preference changes, unranked event ingestion, revisioned durable state |
| Asset service | Catalog, exact-version entitlement, manifests/download descriptors, revocation and update status |
| Asset worker | Quarantine, validation/remediation/provenance; no public ingress or signing private key |
| AI orchestrator | Optional remote intent port only if separately enabled; local model does not require a remote request |
| Telemetry | Privacy-filtered counters and performance segments, never a gameplay dependency |

Use pinned Node.js LTS/TypeScript strict, PostgreSQL/PostGIS, Redis-backed idempotent jobs and Backblaze B2-compatible private object storage/CDN from the baseline. Exact runtime patch, identity provider, deployment topology and region must be recorded in ADR-001/002 and lockfiles during implementation. Those ADRs were still proposed at source review; no deployed topology is asserted here.

## 2. Authentication and content entitlement

**DATA-01.** The server derives the user subject from a validated token, never from request-body `userId`. Every pet, memory, home selection, event stream and asset request is scoped by that subject. Guessing another user's opaque ID must not expose metadata or modify state. Return a generic forbidden/not-found policy consistently.

Use existing `POST /v1/auth/exchange`. Preserve short-lived audience/environment-bound access tokens (≤15 min) and rotating refresh tokens in Keychain/Keystore. Account revocation or deletion cancels refresh and disables subsequent mutation. Initial authentication provider choice remains an implementation decision; gameplay does not embed a provider SDK.

**DATA-02.** Production session entry performs a current online exact-asset-version entitlement check. The already-running session has the existing 72 h revalidation window and up to 24 h unreachable-only grace, bounded by a signed receipt's actual expiry. Explicit `Expired`/`Revoked`/kill switch takes effect on receipt without grace. Never extend a short receipt to 96 h by inference; issue a receipt compatible with policy or expire sooner.

The existing policy constant says online entry while `MayStartSession()` accepts a cached 72 h window, and P0 has development shortcuts. Resolve this discrepancy through ADR-013 and production integration tests. A policy class without enforced network/receipt wiring is not entitlement enforcement.

Poll update state every 15 minutes while active and within one minute of foreground resume, as the existing policy specifies. On successful contact, apply revocations before optional content. Poll errors must not count as a valid entitlement. Persist trusted server check/expiry metadata securely so process death, reboot or clock rollback cannot manufacture a new grace window; if elapsed time cannot be bounded after restart, require an online check. Remote revocations cannot reach an offline device instantly; document that bounded limitation rather than claiming otherwise.

## 3. Durable versus session state

**DATA-03.** Persist the pet relationship and choices. Do not persist local-room coordinates or in-flight mechanics.

| Record | Authority / persistence |
|---|---|
| Pet ID, name, personality seed, asset version | Existing `pets`; server authoritative |
| Confirmed bond/training/preferences | Existing `pet_state` plus explicit preference version; server authoritative |
| Approved favorite toy/trick facts | Existing `pet_memories`; allowlisted, consented, deletable |
| Selected dwelling/catalog version/style | New `pet_dwellings`; no local pose, floor polygon or room identifier |
| Local pending non-spatial changes | Client outbox with idempotency key and base revision |
| Throw velocity/path, pet pose, home occupancy, toy owner | Memory only, discarded on new AR session |
| VPS persistent home | Deferred; later spatial-object route requires processed site and eligible anchor |

### Proposed database migration set

**DATA-04.** Add forward-only migrations after `0003`; do not rewrite applied migrations. Verify existing definitions before extending them.

| Table/change | Fields and invariants |
|---|---|
| `pet_dwellings` | `pet_id` primary key/FK, `catalog_id`, `catalog_version >=1`, bounded `style_json`, `revision`, timestamps; one selection per pet; no coordinates |
| `companion_play_sessions` | opaque `session_id`, owned `pet_id`, server timestamps, `LOCAL_UNRANKED` mode, client/rules versions, next accepted sequence, terminal status |
| `companion_play_events` | `(session_id,event_sequence)` unique, event ID unique per owner, bounded enum/payload, body hash, server receipt time; append-only |
| `idempotency_records` | subject + method + canonical route + key unique; canonical request hash, response status/body, expiry ≥24 h; atomic with mutation |
| `pet_preferences` or typed `pet_state` extension | Allowlisted favorite toy/trick and accessibility presentation settings; reject free-form inferred traits |
| Asset version integrity correction | Review `pet_assets.pet_asset_id UNIQUE` alongside `(pet_asset_id,version)` uniqueness; the existing single-column unique prevents multiple versions of one ID. Migrate deliberately to asset identity + version rows or remove conflicting uniqueness with FK repair |
| Ownership FKs | Add validated composite asset-version references to entitlements and pets after auditing data; don't assume non-FK IDs prove a published version exists |

Every mutable aggregate increments its bigint revision in the same transaction. Enforce status/check constraints and exact event order in the database/service transaction. Application memory or Redis cannot be the durable authority. Cross-device clients cannot update a record by last-writer-wins wall-clock timestamps.

## 4. API additions and compatibility

Existing endpoints retained: `/v1/pets`, `/v1/assets/{petAssetId}/manifest`, `/v1/auth/exchange`, `/v1/ai/intents`, and memory deletion. Generate clients from OpenAPI 3.1 after adding the following definitions; do not hand-maintain parallel client DTOs.

| Proposed endpoint | Request and success | Concurrency/failure |
|---|---|---|
| `GET /v1/companion/bootstrap` | Owned pet read models, selected home, compatible catalog revision, config revision, server time, entitlement/update descriptors; 200 with ETag | Auth required; no signing secrets or room data |
| `GET /v1/pets/{petId}/dwelling` | Current selection and revision; 200 or empty selection | Ownership enforced |
| `PUT /v1/pets/{petId}/dwelling` | `{catalogId,catalogVersion,style}`; 200 updated selection | If-Match + Idempotency-Key; 409 on stale revision; compatibility checked |
| `PATCH /v1/pets/{petId}/preferences` | Strict allowlisted preference fields; 200 new revision | If-Match + Idempotency-Key; unknown fields 422 |
| `POST /v1/companion-play/sessions` | `{sessionId,petId,mode,clientVersion,rulesetVersion}`; 201 receipt/start state | Offline-generated opaque ID accepted only for owned pet; mode fixed LOCAL_UNRANKED; idempotent |
| `POST /v1/companion-play/sessions/{sessionId}/events` | Batch of ≤50 ordered events, ≤64 KiB decoded body; 200 `{acceptedThrough,revision}` | Transactional ordering; duplicates with same hash return prior ACK; changed body conflicts |
| `POST /v1/companion-play/sessions/{sessionId}/finish` | Last sequence and terminal reason; 200 terminal state | One terminal write; repeat identical finish idempotent |
| `GET /v1/client/update-state` | Config/kill switches/revocations/catalog revision and entitlement status | ETag; foreground/update polling; validate account independently of cached response |

HTTP envelope uses existing `error.code`, localization-friendly message, requestId, retryable and bounded details. Add explicit `SESSION_SEQUENCE_GAP`, `CONTENT_INCOMPATIBLE`, `EVENT_EXPIRED` and `PAYLOAD_INVALID` definitions when contracts land. Never reuse `ASSET_SIGNATURE_INVALID` for a recoverable pathfinding issue.

Bodies reject unknown security-sensitive fields. Require RFC3339 UTC strings where transport time is needed, bounded strings/enums, finite numeric ranges, normalized rotations only in permitted future spatial routes, and valid opaque IDs. The examples below are proposed DTO shapes, not signed production manifests.

```json
{
  "schemaVersion": 1,
  "eventId": "evt_01J8ZQK5T7VN2MXR4WD6GHYAB3",
  "eventSequence": 1,
  "roundId": "rnd_01J8ZQK5T7VN2MXR4WD6GHYAB3",
  "type": "FETCH_COMPLETED",
  "elapsedTicks": 430,
  "rulesetVersion": 1,
  "origin": "LOCAL_UNRANKED"
}
```

Introduce `evt_`, `rnd_`, and `cps_` prefixes in shared ID validation/contracts as part of this migration; current `GibiId` prefix allowlists must not silently reject new identifiers. Prefixes denote different entity types even if example ULID suffixes match.

No API accepts arbitrary `bondDelta`, inventory quantity, score, world pose, raw gesture stream, camera frame, or psychological estimate from this event. Unranked client observations are not trusted competitive proof.

## 5. Synchronization and rewards

**DATA-05.** The local outbox appends before acknowledging a durable preference change in UI. Entries contain entity, operation, base revision, immutable canonical payload hash and idempotency key. Store acknowledged read-model state separately from pending operations. Use atomic save replacement and schema migrations; corruption falls back to server recovery without fabricating state.

For session events: persist session-create before events, send sequential batches, and finish after all prior sequences ACK. Reconnect sends the same keys/bodies. A gap response includes the next expected sequence; replay from the durable outbox rather than skipping. An already accepted event with a new body is a conflict and must not advance the stream.

Initial outbox cap: 1,000 events or 5 MiB, seven-day maximum age. Preserve pending explicit preference/deletion operations; compact or discard oldest nonessential play telemetry under pressure. Never delete a middle event then send a gapped authoritative stream: mark that unsynced play session abandoned and upload only an explicitly defined non-authoritative summary if desired. Expired events cannot mint rewards. Surface a settings sync issue only in the relevant UI; fetch continues.

Mutations use existing backoff 0.5/1/2/4 s plus 0–250 ms jitter, max four retries for eligible idempotent operations, honoring Retry-After. A fresh user change receives a new key. Never retry signature failures or changed-body idempotency conflicts automatically.

**DATA-06.** Keep fetch unranked and nonmonetary. In v1, FETCH_COMPLETED can update internal activity/approved preference suggestions but grants no coins or paid inventory. If bond progression is enabled, derive a bounded server rule from accepted, deduplicated session events and expose confirmed state only; do not trust client elapsedTicks as evidence of real play. An offline client may show local praise but labels any pending durable change internally until server confirmation.

For two-device preference conflicts, refresh the authoritative revision and present/reapply the user's latest explicit selection after compatibility validation. Do not merge quantities or inferred preferences automatically. Delete/tombstone operations win over stale queued creates for the same memory.

## 6. AI intent and memory services

**AI-01.** Reuse `ai-intent.schema.json` revision 2 and the published intent catalog. Add a real validator, because `AiSupplementPolicy.Resolve()` only checks a subset (known intent, context revision and lateness). The final validator must also enforce request/pet ownership, schema/catalog revision, target membership/type, expiry, source age, bounds, and current action generation. Unknown properties are rejected.

Minimal context: opaque pet/player identity scoped to provider, seed-derived game personality, approved game memories, allowed intents and target IDs/types, coarse local time bucket, and current game state. Exclude camera/depth/room data, precise coordinates, voice audio, contacts, medical/sensitive or inferred user traits. Care flags stay inside the policy boundary; they are not model prompt context.

**AI-02.** Local model runtime is an independently gated implementation track:

1. Implement null `IIntentSource` and full invalid/late-output tests.
2. Select one licensed model artifact and one CPU runtime using measured device results. Pin digest, license, tokenizer, quantization, compiler, runtime and target ABI. Do not equate a parameter count with a guaranteed resident-memory size.
3. Constrain output with a grammar/schema encoder and semantic validation. The 512 input /48 output token caps in GW-ARCH-002 require a compact structured encoding: a full verbose JSON envelope may exceed 48 tokens. Have the model emit an intent/target index and bounded modifier tokens; the trusted adapter constructs IDs, timestamps and the revision-2 envelope. Validate equivalence and prevent index access outside the submitted catalog.
4. Keep one request outstanding on a worker thread. On stop/background/low memory, disable acceptance within one 10 Hz tick, cancel inference and asynchronously release native memory; never block the render thread waiting for native teardown. Actual memory reclamation latency is measured and must meet OS pressure tests. Amend any impossible synchronous-unload promise before release.
5. Enforce existing total app memory ceilings. GW-ARCH-002 budget caps are A ≤520 MiB brain and B ≤230 MiB brain, C disabled, all inside app memory. A nominal 1B int4 artifact alone is roughly 500 MB before runtime overhead, so do not assume it fits the document's 420 MiB weight allocation. Choose an artifact that measures within every cap or revise the allocation explicitly.
6. Add GPU/NPU backends only after runtime/operator/device support is verified. Android Tensor/NPU availability and an accelerator label do not guarantee this model or decoder can execute there. Benchmark sustained AR camera + render + inference, not isolated inference.
7. Ship only if the supplement improves reviewed behavior quality while all local capability, thermal, memory and latency gates still pass. Otherwise leave it disabled without weakening the complete companion.

Signed model distribution reuses canonical signature/digest verification, with artifact-type-specific parsing and schemas; do not reuse a GLB loader to parse model bytes. Require model/catalog compatibility and a revocation path. Preserve GW-ARCH-002 platform distribution intent, but verify current platform delivery constraints during implementation; no model is bundled or downloaded by this documentation task.

**AI-03.** Memory proposals use only existing FAVORITE_TOY, PREFERRED_TRICK, PLAY_TIME_OF_DAY and FAVORITE_PLACE_TAG fact types, with catalog-backed values and user consent. No inferred personal facts. A deletion immediately suppresses local use, resets model context/KV data, writes a tombstone and schedules server deletion; server context exclusion occurs within 24 h. Stale outbox proposals cannot resurrect a deleted fact.

## 7. Privacy and operational controls

**OPS-01.** No camera images, raw room mesh/depth, precise placement coordinates, raw microphone audio, gesture traces, tokens or signed URLs in analytics. Use bounded event categories such as fetch_started/completed/recovered, recovery_reason, home_entry_blocked, anchor_degraded, asset_validation_failed, frame/thermal tier, and intent_rejected_reason. Keep session/round identifiers ephemeral or pseudonymous with controlled cardinality.

Inherited retention: raw telemetry 30 days, aggregates 13 months; approved memories until deletion. Account deletion removes active personal state, revokes credentials, clears device cache on next contact and propagates tombstones to provider context/jobs. Document backup retention and deletion behavior; encrypted backups require the baseline 35-day retention and tested restoration. Do not promise instantaneous erasure from immutable backups.

**OPS-02.** Proposed flags: `companion_fetch_v1`, `dwelling_entry_v1`, `local_brain_enabled`, `constrained_practice_enabled`, and catalog/profile revocation lists. Store their defaults and schema in client build config. Production defaults keep optional model and unadopted constrained practice off. Flag changes cannot expand an incompatible asset's capabilities or bypass verification. Invalidate/disable on next successful update poll; offline limitations follow DATA-02.

Maintain baseline core API availability ≥99.9%, manifest p95 ≤300 ms, crash-free sessions ≥99.7%, and optional AI p95 ≤2.5 s without blocking play. Measure frame/thermal data per client/device tier. Zero expected asset-integrity failures merits immediate investigation. On a bad release, disable affected optional behavior/content, revert to last compatible verified artifact, and halt rollout; do not overwrite an immutable version.

Use environment-separated keys/catalogs, least-privilege worker access, audit records for publishing/revocation, bounded job concurrency and a dead-letter queue. Asset-ready/webhook ingestion is at-least-once and idempotent. Restore tests must demonstrate the baseline RPO 15 min/RTO 2 h; daily backups alone cannot meet a 15-minute RPO without WAL/PITR or equivalent.

Under-13 and care-context availability remain gated by accepted product policy and completed end-to-end controls, not just migration `0003`. No new legal determination is made here. Existing proposed ADR legal statements need review by the release owner before adoption.
