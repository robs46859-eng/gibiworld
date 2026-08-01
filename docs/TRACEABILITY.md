# GW-* Requirement Traceability Matrix

Binds every requirement in GW-ARCH-001 section 17 to an artifact and an acceptance test.
Per section 19: **a missing result is a failed release gate.**

Status legend: `IMPL` implemented + test written · `PART` partially implemented ·
`TODO` not started · `BLOCKED` waiting on an external dependency

## AR / Spatial

| Req | Normative behavior | Implementation | Test | Status |
|---|---|---|---|---|
| GW-AR-001 | Exactly one ARSession and XR Origin in ARWorld | `Editor/SceneValidator.cs` | automated scene validation | TODO |
| GW-AR-002 | Ranked solid + scoring only while anchor tracked | `Spatial/AnchorEligibility.cs` | `GW_AR_002_003_AnchorGating` | IMPL |
| GW-AR-003 | Tracking loss pauses ranked time within 250 ms | `AnchorEligibility.DegradeGraceMs` | `Tracking_loss_degrades_and_pauses_within_250ms` | IMPL |
| GW-AR-004 | Local anchor never reaches persistence endpoint | `AnchorEligibility.MayPersistPlacement` + `spatial-object.schema.json` | `Local_anchor_never_authorises_persistence` | IMPL |
| GW-AR-005 | Anchor-local quaternion normalized within 1e-4 | `Core/AnchorLocalPose.cs` | `GW_AR_005_QuaternionNormalisation` (5 cases) | IMPL |
| GW-AR-006 | Hazard semantic regions reject destinations/obstacles | `Spatial/SurfaceAcceptance.cs` | `Hazard_tags_reject_placement` | IMPL |
| GW-AR-007 | Slope and clearance gates match thresholds | `SurfaceAcceptance` 12°/7°/1.5 m/2.0 m | `Ranked_gates_use_the_stricter_seven_degree_slope` | IMPL |
| GW-AR-008 | Relocalization moves course children ≤ 2 cm | floating-origin recentering | mapped-site device test | TODO |

## Asset trust

| Req | Normative behavior | Implementation | Test | Status |
|---|---|---|---|---|
| GW-ASSET-001 | Unknown issuer/key/schema/species/rig rejects before render | `AssetVerifier` steps 1–3 | tampered fixture matrix | PART |
| GW-ASSET-002 | Digest mismatch deletes temp bytes, no URL retry | `AssetVerifier` step 5 | integration test | PART |
| GW-ASSET-003 | Expired/revoked entitlement blocks instantiation | `IEntitlementGate` + `pet_entitlements` | auth integration test | PART |
| GW-ASSET-004 | External URI in GLB rejects | `glb_inspect.py` EXTERNAL_URI + `AssetLimits.RejectParsed` | malicious GLB fixture | PART |
| GW-ASSET-005 | Geometry/texture/bone/material/clip limits server AND client | `glb_inspect.py` + `AssetLimits` | boundary fixtures | IMPL |
| GW-ASSET-006 | No shader/script/animation-event execution | `AssetLimits.RejectParsed` + import policy | static + runtime inspection | PART |
| GW-ASSET-007 | Cache promotion atomic, keyed by SHA-256 | `IQuarantineCache.PromoteAtomicAsync` | crash-injection test | PART |
| GW-ASSET-008 | Preset and Pawsome3D use the SAME verifier | single `AssetVerifier`, issuer is data not branch | end-to-end test | IMPL |

## Gameplay

| Req | Normative behavior | Implementation | Test | Status |
|---|---|---|---|---|
| GW-GAME-001 | Safety override interrupts all lower priority in one 10 Hz tick | `Pets/BehaviorArbiter.cs` | `GW_GAME_001_SafetyOverride` (4 cases) | IMPL |
| GW-GAME-002 | Locomotion deterministic across 30/60/120 fps | `Pets/DeterministicMotion.cs` | `Locomotion_distance_is_identical_at_30_60_and_120_fps` | IMPL |
| GW-GAME-003 | Gate crossing uses swept volume and exact order | `Gameplay/GateCrossing.cs` | `GW_GAME_003_SweptGateCrossing` (3 cases) | IMPL |
| GW-GAME-004 | Backgrounding makes active ranked run unranked | lifecycle handler | lifecycle test | TODO |
| GW-GAME-005 | Offline supports placement/cues/training/fetch/practice | local behavior library | airplane-mode playthrough | PART |
| GW-GAME-006 | No absence duration decreases bond or inventory | `MonotonicClock` (no wall-clock in scoring path) | clock-jump + 90-day absence test | PART |
| GW-GAME-007 | Course versions immutable after publication | `course_versions_immutable` trigger | database constraint test | IMPL |
| GW-GAME-008 | Degradation allows practice finish, no leaderboard write | `ScoringMode.Paused` + `ranked` column | end-to-end test | PART |

## AI

| Req | Normative behavior | Implementation | Test | Status |
|---|---|---|---|---|
| GW-AI-001 | Output validates against fixed schema and intent enum | `ai-intent.schema.json` | fuzzed provider output | PART |
| GW-AI-002 | AI cannot select animation/coords/physics/value/score/moderation | `additionalProperties:false` + enum-only intent | schema negative tests | IMPL |
| GW-AI-003 | Expired/stale-revision/wrong-target intent rejected | `contextRevision` + `expiresAt` required | unit test | PART |
| GW-AI-004 | AI timeout silently invokes local fallback | `BehaviorArbiter` CALM_IDLE default | dependency fault injection | PART |
| GW-AI-005 | Request excludes location/camera/tokens/contacts/voice | `gw-ai-orchestrator` egress allowlist | egress assertion test | TODO |
| GW-AI-006 | Deleted memory absent from AI context within 24 h | `pet_memories` tombstone + partial index | deletion workflow test | PART |

## API

| Req | Normative behavior | Implementation | Test | Status |
|---|---|---|---|---|
| GW-API-001 | OpenAPI 3.1 rejects invalid security-sensitive fields | `additionalProperties:false` throughout | contract test | IMPL |
| GW-API-002 | Idempotency returns original / conflicts on changed body | `Idempotency-Key` parameter on all value POSTs | concurrency test | PART |
| GW-API-003 | Revision conflict returns 409 without partial mutation | `revision` columns + `If-Match` | transactional test | PART |
| GW-API-004 | Client retries only approved failures with bounded jitter | retry policy 0.5/1/2/4 s + ≤250 ms jitter, max 4 | network proxy test | TODO |
| GW-API-005 | Tokens and signed URLs never in logs or crash payloads | `AssetVerifier` step 8 comment + telemetry filter | redaction canary | PART |
| GW-API-006 | Inventory ledger cannot go negative under concurrency | `inventory_no_negative` constraint trigger | load/property test | IMPL |

## Security

| Req | Normative behavior | Implementation | Test | Status |
|---|---|---|---|---|
| GW-SEC-001 | TLS 1.3 + valid certs on production endpoints | infra | external scanner | TODO |
| GW-SEC-002 | Refresh rotates on use; reuse revokes family | `/v1/auth/exchange` | security integration test | TODO |
| GW-SEC-003 | Webhook timestamp window + replay protection | `webhookHmac` + 5-min window | replay test | PART |
| GW-SEC-004 | Production buckets private, listing disabled | OpenTofu B2 policy | cloud policy test | TODO |
| GW-SEC-005 | Admin needs SSO + hardware MFA + RBAC + audit | `gw-admin` + `audit_log` rules | access review | PART |
| GW-SEC-006 | Telemetry filter strips coordinates and credentials | `gw-telemetry` PII filter | PII canary | TODO |
