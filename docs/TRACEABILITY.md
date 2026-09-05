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

## GW-ARCH-003 — 39 Implementation Requirements

| Req ID | Requirement Summary | Implementation Path | Acceptance Test | Status |
|---|---|---|---|---|
| AR-01 | Measured/Unknown states for lighting & headroom | `Gibi.Core/Runtime/SpatialTypes.cs`, `ARSurfaceProbe.cs` | `SpatialMeasurementTests` | IMPL |
| AR-02 | Full composition footprint & polygon distance | `Gibi.Spatial/Runtime/SurfaceAcceptance.cs` | `PlacementPolygonTests` | PART |
| AR-03 | Slope $\le 12^\circ$ & non-ranked separation | `Gibi.Spatial/Runtime/SurfaceAcceptance.cs` | `SurfacePurposeTests` | IMPL |
| AR-04 | Local grid navigation & swept corridor | `Gibi.Spatial/Runtime/LocalGridNavigation.cs` | `LocalGridNavigationTests` | IMPL |
| AR-05 | Tracking loss stop & 250 ms degrade | `Gibi.Spatial/Runtime/AnchorEligibility.cs` | `TrackingRecoveryTests` | IMPL |
| PET-01 | 10 Hz perception/policy loop & stable seed | `Gibi.Pets/Runtime/BehaviorArbiter.cs`, `PetController.cs` | `PetPolicyScenarioTests` | PART |
| PET-02 | ActionToken generation & sequence guards | `Gibi.Core/Runtime/ActionToken.cs`, `BehaviorArbiter.cs` | `ActionTokenTests` | IMPL |
| PET-03 | Null intent source fallback & local parity | `Gibi.AI/Runtime/NullIntentSource.cs` | `IntentEnvelopeTests` | IMPL |
| PET-04 | 50 Hz motion accumulator & gait exertion | `Gibi.Pets/Runtime/DeterministicMotion.cs`, `PetController.cs` | `MotorReplayTests` | IMPL |
| FETCH-01 | Player-initiated throw; demo director disabled in prod | `Gibi.Gameplay/Runtime/FetchSession.cs`, `SceneBuilder.cs` | `PlayerFetchTests` | IMPL |
| FETCH-02 | Fixed-step throw solver (apex $\le 0.8\text{m}$, speed $\le 6\text{m/s}$) | `Gibi.Gameplay/Runtime/ThrowSolver.cs` | `ThrowSolverTests` | IMPL |
| FETCH-03 | Complete fetch state machine & timeout recoveries | `Gibi.Gameplay/Runtime/FetchSession.cs` | `FetchPhaseTests` | IMPL |
| FETCH-04 | Single toy transform owner & jaw socket attachment | `Gibi.Pets/Runtime/ToyController.cs` | `ToyOwnershipTests` | IMPL |
| FETCH-05 | Bounded return zone & player movement tracking | `Gibi.Gameplay/Runtime/FetchSession.cs` | `ReturnZoneTests` | IMPL |
| FETCH-06 | Release confirmation, idempotent completion, no score | `Gibi.Gameplay/Runtime/FetchSession.cs` | `FetchCompletionTests` | IMPL |
| HOME-01 | Fitted doorway ($\ge 0.70 \times 0.90\text{m}$) & interior envelope | `Gibi.Pets/Runtime/DwellingDefinition.cs` | `DwellingInteractionTests` | IMPL |
| HOME-02 | Traversable entry/turn/rest/exit; no concealment | `DwellingDefinition.cs`, `DwellingInteraction.cs` | `DwellingInteractionTests` | IMPL |
| HOME-03 | Edit-mode dwelling relocation; no pose serialization | `Gibi.Pets/Runtime/DwellingInteraction.cs` | `DwellingEditTests` | PART |
| SYS-01 | Idempotent cancellation across all phases | `FetchSession.cs`, `ToyController.cs`, `DwellingInteraction.cs` | `InterruptionMatrixTests` | IMPL |
| SYS-02 | Accessible UI, touch target sizes, UI touch exclusion | `Gibi.UI/Runtime/CompanionInputRouter.cs` | `AccessibilityWalkthrough` | IMPL |
| ASSET-01 | Production assets and manifest tracking | `contracts/schemas/pet-manifest.schema.json` | `ReleaseCatalogCoverage` | PART |
| ASSET-02 | Physical scale (shoulder 0.50 m, ball 0.067 m) | `PetAnimationProfile.cs`, `ThrowSolver.cs` | `PhysicalScaleFixtures` | PART |
| ASSET-03 | Generic rig & bone limits ($\le 96$ deform bones) | `PetAnimationProfile.cs` | `RigDeformationFixtures` | PART |
| ASSET-04 | Trusted contact profiles & action timing | `PetAnimationProfile.cs` | `ContactProfileFixtures` | PART |
| ASSET-05 | Native clip library; P0 substitutes rejected | `PetAnimationProfile.cs` | `NativeClipCoverage` | PART |
| ASSET-06 | Generic animation graph & offscreen advancement | `PetAnimator.cs`, `PetController.cs` | `AnimationIntegrationTests` | PART |
| ASSET-07 | Asset verifier & quarantine cache | `Gibi.AssetRuntime/Runtime/AssetVerifier.cs` | `AssetTrustMatrix` | IMPL |
| ASSET-08 | Catalog integrity & signed envelopes | `Gibi.AssetRuntime/Runtime/PresetCatalog.cs` | `CatalogIntegrityTests` | IMPL |
| DATA-01 | Token subject scoping & cross-user isolation | `contracts/openapi/gibiworld.v1.yaml` | `OwnershipIsolationTests` | PART |
| DATA-02 | Session entry exact-version entitlement check | `Gibi.AssetRuntime/Runtime/ConnectivityPolicy.cs` | `EntitlementLifecycleTests` | PART |
| DATA-03 | No anchor-local/world coordinates in durable saves | `db/migrations/0004_companion_dwellings_sessions_events.sql` | `PersistenceBoundaryTests` | IMPL |
| DATA-04 | Migration forward repair: asset-version uniqueness & FKs | `db/migrations/0004_companion_dwellings_sessions_events.sql` | `MigrationInvariantTests` | IMPL |
| DATA-05 | Offline outbox, retry with jitter, gap handling | `Gibi.Networking/Runtime/OfflineOutbox.cs` | `OfflineReplayTests` | IMPL |
| DATA-06 | Unranked fetch; no client currency/bond grants | `FetchSession.cs`, `db/migrations/0004_*.sql` | `NoClientGrantTests` | IMPL |
| AI-01 | Intent schema validation & allowlist enforcement | `Gibi.AI/Runtime/NullIntentSource.cs` | `IntentEnvelopeTests` | IMPL |
| AI-02 | Model memory/thermal benchmark gates | Gated to M6 | `ModelBudgetBenchmarks` | BLOCKED |
| AI-03 | Memory deletion & tombstone within 24 h | `db/migrations/0002_world_courses_ledger.sql` | `MemoryDeletionTests` | PART |
| OPS-01 | Telemetry privacy filter (no camera/coordinates) | `Gibi.Telemetry/` | `TelemetryPrivacyTests` | PART |
| OPS-02 | Feature flag & content rollback drill | `Gibi.Core/Runtime/DeviceTier.cs` | `RollbackDrill` | PART |
