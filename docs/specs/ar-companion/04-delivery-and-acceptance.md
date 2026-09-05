# Delivery and acceptance — GW-ARCH-003 v1.0.0

Parent: [architecture](../../GW-ARCH-003-AR-Companion-Build-Specification.md). This is the engineering execution backlog. All work below is **planned**, unless explicitly described as existing source. A document, generated scene, or test name alone does not discharge a gate.

## 1. Work sequence and ownership

Recommended team: one lead/Unity gameplay engineer, one AR/client engineer, one backend engineer, one technical artist/animator, and part-time device QA. Roles can be combined; art and physical-device time remain real dependencies. This is a staffing assumption, not a commitment that people have been assigned.

| Milestone | Deliverables | Dependencies | Demonstrable exit |
|---|---|---|---|
| M0 — Establish reproducible baseline | Confirm spec/ADR precedence; clean toolchain restore; production build guard; refreshed evidence inventory; fixtures | None | Fresh Android development build and baseline tests from one commit, documented iOS provider gap |
| M1 — Interaction on primitives | Player command router, throw preview/flight, round FSM, path grid, ownership and cancellation | M0 | User performs ten full throws in Editor simulation without demo automation |
| M2 — Final pet and dwelling assets | Re-skinned dog, native core clips, measured home interior/door, ball, contact profiles | M0; runs alongside M1 | Reopened signed candidate assets pass geometry/rig/contact/home fixtures |
| M3 — Integrated AR companion | Real asset animation, dwelling entry/exit, player movement return, tracking recovery, capability modes | M1 + M2 | Complete manual loop on reference Android with no substitutes or concealed occupancy |
| M4 — Production identity and persistence | Auth, entitlement, catalog, owned state, outbox, memory deletion, service telemetry | M0; can run alongside M1–M3 | Two-device state/retry/revocation tests; no client-created value; deployed staging evidence |
| M5 — iOS parity and release hardening | ARKit setup, device matrix, accessibility, thermal/memory, content rollback, store build provenance | M3 + M4 | Every required acceptance row passes on Android and iOS release candidates |
| M6 — Optional model supplement | Null port → model/backend benchmark → constrained intent adapter → feature-flagged pilot | M3 + M4 and ADR-013 adoption | Measured quality improvement without degrading base loop or budgets |

Illustrative effort range for M0–M5: **45–75 person-weeks**, roughly **10–16 elapsed weeks** with the assumed team and overlapping art/backend work. These are preliminary planning estimates from a source review, not a schedule promise. Re-estimate after M0 and the first native animation/home-fit fixture. Optional model engineering can add 4–8 person-weeks plus device-specific accelerator work; it is excluded from the base release critical path under proposed ADR-013.

The critical path is reproducible build → trustworthy spatial/motor loop → native contact animation and fitted dwelling → device integration → production trust/sync → both-platform acceptance. Adding more behavior labels does not shorten that path.

## 2. File-level implementation backlog

Paths below are relative to repository root. *New* means create it during implementation; it is not present because this architectural package names it.

| Work ID | Owner | Existing integration point / new artifact | Required change |
|---|---|---|---|
| W01 | Lead/client | `Assets/Gibi/Editor/SceneBuilder.cs`, `SceneValidator.cs`, new `BuildGuard.cs` | Generate production/dev scenes explicitly; validate one provider/session/origin/pose driver/listener, no production demo, manifest/settings agreement |
| W02 | AR | `Assets/Gibi/Spatial/Runtime/ARSurfaceProbe.cs`, `ISurfaceProbe.cs` | Replace constant metrics with measured/unknown values; polygon distance, obstacle/headroom/light evidence |
| W03 | AR | `ARSessionDriver.cs`, `ARWorldAnchorHost.cs`, `AnchorEligibility.cs` | Snapshot revisions; first-loss stop; recovery/cancel; provider cleanup and stale-callback protection |
| W04 | AR | new Core navigation ports and `Spatial/Runtime/LocalGridNavigation.cs` | Bounded grid/A*, stable ties, capsule corridor, doorway path, revisioned rebuild |
| W05 | Gameplay | `Gameplay/Runtime/PlacementController.cs`, `P0SessionDriver.cs`; new `CompanionSessionCoordinator.cs` | Full-composition validation, atomic placement, startup/shutdown ownership, provider-neutral composition |
| W06 | Gameplay/UI | new `UI/Runtime/CompanionInputRouter.cs`, `FetchAimView.cs`; adapt `TapToPlace.cs` | State-dependent input, UI interception, drag/tap throw, command buttons, localization and accessibility |
| W07 | Gameplay | new `Gameplay/Runtime/FetchSession.cs`, `ThrowSolver.cs`; adapt `Pets/Runtime/FetchSequence.cs` | Full fetch round including aim/flight/recovery; revision/token guards; exact terminal semantics |
| W08 | Pet/client | `FetchToy.cs`; new `ToyController.cs`, `PetMotor.cs`, `PetActionTimeline.cs` | Single transform owner, 50 Hz path motion, contact timing, safe detach and cancellation |
| W09 | Pet/client | `PetController.cs`, `BehaviorArbiter.cs`, `IntentPolicy.cs`, `PetBiometrics.cs` | Extract coherent executors, action tokens, fatigue affects manner only, available-capability filtering |
| W10 | Pet/client | `RestAffordance.cs`, `AffordanceRegistry.cs`; new `DwellingInteraction.cs`, `DwellingDefinition.cs` | Fit/occupancy/reservations, real entry/turn/rest/exit, edit-mode relocation, no concealment shortcut |
| W11 | Animation/client | `PetAnimator.cs`, `PetAnimationProfile.cs`, `AssetRuntime/Runtime/PetAssetLoader.cs` | One validated Generic import/playback/rigging route; native clips, action contact profiles, offscreen independence |
| W12 | Technical art | new production derivatives plus worker profiles | Smooth rig, native clip library, fitted house, ball LODs, sockets, collision and measured dimensions |
| W13 | Asset/backend | `tools/gw-asset-worker/`, `contracts/schemas/pet-manifest.schema.json` | Bounded import fixtures, versioned multi-file/profile trust, production signing, catalog release records |
| W14 | Backend | new `services/gw-edge-api`, `gw-game-service`, `gw-asset-service` | Implement existing/additive APIs, owner checks, entitlement/update state, idempotency and read models |
| W15 | Backend | `contracts/openapi/gibiworld.v1.yaml`, new schemas, `db/migrations/0004+` | Exact contracts and new IDs; asset-version key correction; dwelling/session/event/idempotency tables |
| W16 | Client/backend | `Networking/Runtime/` plus `ConnectivityPolicy.cs` integration | Generated clients, secure receipts, persistent outbox, replay/conflict handling and revocation |
| W17 | AI | `AI/Runtime/` new context/validator/null-source classes | Complete envelope semantic checks; local behavior remains independently complete |
| W18 | QA/ops | `.github/workflows/`, new staging manifests/runbooks, telemetry | Test pipeline, physical device evidence, release flag/rollback drill, fresh traceability |
| W19 | AI/native | later `Gibi.AI.LocalBrain` and signed model schema/artifacts | Benchmark and implement constrained model supplement only after M6 gates |

For Unity rows, `Assets/` is under `clients/gw-mobile/`. Each change must maintain the actual asmdef graph. New Core interfaces use Core value types; a namespace alone does not grant assembly access. Update `tools/check_assembly_refs.py` only when the allowed graph changes through an adopted decision, not to silence an accidental dependency violation.

### Small implementation slices

1. Add a player Fetch button that starts existing fetch once; disable automatic director in the production composition. Retain explicit demo fixtures.
2. Add aim/throw flight with a primitive ball, fixed support polygon and fake spatial provider.
3. Add token-owned pickup/drop timeline and assert cancellation in every phase.
4. Add grid pathing around a primitive dwelling with a real opening.
5. Bind final Generic dog and contact metadata; complete visible entry/rest/exit.
6. Run the same loop with actual AR snapshots and tracking recovery.
7. Add production session-entry trust and durable state synchronization.

These are reviewable increments toward the complete target. None is labelled the complete release until the relevant final art/device/service gates pass.

## 3. Requirement-to-evidence matrix

Test names are proposed acceptance fixtures; no results are claimed here. Bind them into existing `docs/TRACEABILITY.md` when implemented, retaining the parent GW-* bindings.

| Requirement | Acceptance fixture / observable result | Milestone |
|---|---|---|
| AR-01 | `SpatialMeasurementTests`: unknown/NaN/light/headroom cannot pass; measured values affect eligibility | M1/M3 |
| AR-02 | `PlacementPolygonTests`: center and edge hits differ correctly; preview/commit match; depth cannot replace classified hit | M1/M3 |
| AR-03 | `SurfacePurposeTests`: slopes and pet/course limits separated; gaps/stairs rejected | M1 |
| AR-04 | `LocalGridNavigationTests`: complete swept corridor, deterministic tie break, concave boundaries, no diagonal corner cuts, stale path rejected | M1 |
| AR-05 | `TrackingRecoveryTests`: immediate stop on first loss, 250 ms presentation degrade, 1 s stable resume, >3 s cancellation, no pose-loss ghost throw | M3 |
| PET-01 | `PetPolicyScenarioTests`: identical seed/snapshot produces same choices; direct cues available with zero energy; all enabled intents execute | M1/M3 |
| PET-02 | `ActionTokenTests`: stale same-named action completion cannot clear new action; one terminal event | M1 |
| PET-03 | `IntentSupplementTests`: absent/late/malformed source leaves local gameplay identical in capability | M1/M6 |
| PET-04 | `MotorReplayTests`: same input/snapshot replay at 30/60/120 render fps gives same action order and ≤2 mm pose difference | M1/M3 |
| FETCH-01 | `PlayerFetchTests`: no spontaneous production throw; one real gesture/button starts one round | M1/M3 |
| FETCH-02 | `ThrowSolverTests`: preview endpoint equals flight/settle endpoint ≤1 cm; speed/apex/swept obstacles bounded | M1 |
| FETCH-03 | `FetchPhaseTests`: every legal phase plus timeout/suspend/cancel route terminates coherently | M1/M3 |
| FETCH-04 | `ToyOwnershipTests`: exactly one renderer/owner; jaw contact, LOD and cancellation do not duplicate or lose toy | M2/M3 |
| FETCH-05 | `ReturnZoneTests`: moving user updates bounded target; out-of-area user never pulls dog out of play space | M3 |
| FETCH-06 | `FetchCompletionTests`: release/support required, duplicate callback not rewarded, repeat immediately available | M1/M4 |
| HOME-01 | `DwellingFitFixtures`: opening/entry/turn/rest envelopes fit both eligible dogs; undersized home rejected | M2 |
| HOME-02 | `DwellingSequenceTests`: visible continuous entry/down/rise/exit; occupancy released on cancel/destroy | M3 |
| HOME-03 | `DwellingEditTests`: cannot move occupied home; invalid footprint rejected; local pose never serialized | M3/M4 |
| SYS-01 | `InterruptionMatrixTests`: every listed system event tested in every fetch/home phase; idempotent cleanup | M3 |
| SYS-02 | `AccessibilityWalkthrough`: single-tap mode, target sizes, caption/icon cues, reduced motion, UI touch exclusion | M5 |
| ASSET-01 | `ReleaseCatalogCoverage`: hero/second fixture/home/toy/UI/audio/effects and source licenses present | M2 |
| ASSET-02 | `PhysicalScaleFixtures`: imported scale matches declared physical size and measured full envelope | M2 |
| ASSET-03 | `RigDeformationFixtures`: required chain and smooth reviewed weights; no fake coverage-only rig | M2 |
| ASSET-04 | `ContactProfileFixtures`: profile bound to exact clip/digest, events only trusted enums, contact tolerances pass | M2 |
| ASSET-05 | `NativeClipCoverage`: required profile clips native; P0 substitutes rejected in release hero | M2 |
| ASSET-06 | `AnimationIntegrationTests`: Generic graph + IK/gaze work; offscreen action progression; root motion absent | M3 |
| ASSET-07 | `AssetTrustMatrix`: bad signatures/digests, URI/size/decode limits, owner mismatch, worker timeout all reject | M4 |
| ASSET-08 | `CatalogIntegrityTests`: tampered prop/profile, incompatible version and revoked digest never render | M4 |
| DATA-01 | `OwnershipIsolationTests`: cross-user reads/writes and wrong token audience/expiry rejected | M4 |
| DATA-02 | `EntitlementLifecycleTests`: entry check, 72 h/24 h boundary, receipt expiry, revoke, restart/rollback and foreground poll | M4 |
| DATA-03 | `PersistenceBoundaryTests`: no anchor-local/world pose or room geometry in local save/event APIs | M4 |
| DATA-04 | `MigrationInvariantTests`: multiple asset versions, FK ownership, revision increment and event uniqueness enforced | M4 |
| DATA-05 | `OfflineReplayTests`: crash at append/send/ACK, duplicate retry, gap, seven-day expiry and outbox cap | M4 |
| DATA-06 | `NoClientGrantTests`: forged score/bond/currency fields rejected; duplicates produce one allowed durable effect | M4 |
| AI-01 | `IntentEnvelopeTests`: every schema/target/catalog/owner/expiry invalidity rejected | M4/M6 |
| AI-02 | `ModelBudgetBenchmarks`: measured license/artifact/runtime compatibility, full AR thermal/memory/latency envelope | M6 |
| AI-03 | `MemoryDeletionTests`: local immediate suppression, server ≤24 h exclusion, no stale-event resurrection | M4/M6 |
| OPS-01 | `TelemetryPrivacyTests`: prohibited data absent before export; deletion and retention workflows exercised | M4/M5 |
| OPS-02 | `RollbackDrill`: flags and content revocation work on contact; compatible rollback; offline limitation visible in ops evidence | M5 |

## 4. Test strategy

### Pure logic and fixture tests

Run fast EditMode tests for throw math, path planning, boundary predicates, action tokens, state machines, save transitions and typed envelope validation. Property tests cover arbitrary finite points, invalid values, degenerate polygons, duplicate/cancelled events and all command orderings. Compare fixed simulation results while varying render rate; record sensor snapshots rather than expecting two live AR captures to match.

Add PlayMode tests only where Unity integration matters: generated scene bootstrap, Generic animation/rigging, signed model binding, contact sockets, pooled object ownership, additive scene teardown and resource disposal. Keep a fake provider test scene for every developer; it does not replace devices.

### AR/device matrix

| Device class | Minimum fixture | Required observations |
|---|---|---|
| Reference Android | Pixel 9a from historical handoff, if available | Cold launch, camera motion, floor placement, complete fetch/home, no prior native error regression |
| Android without depth | Actual supported ARCore handset selected in M0 | Honest capability mode, no manufactured headroom/obstacles, core UI parity |
| Android performance floor | Lowest shipping RAM/SoC tier selected in M0 | 30 fps mode, memory ceiling, twenty-minute thermal session |
| iPhone without LiDAR | Actual supported ARKit device selected in M0 | Provider startup, placement, recovery and feature fallback |
| iPhone/iPad with depth | Actual supported device selected in M0 | Occlusion alignment, person/obstacle crossings, full composition and path behavior |

No minimum OS beyond the existing build baseline is invented here. Android source/handoff uses min SDK 29 and target 36; implementation must check current store requirements when preparing submission and record any required version change. iOS minimum/device floor is a M0 measurement/compatibility decision. Vendor device support must be checked live before finalizing the release matrix.

Rooms: textured rug, low-texture floor, clutter near doorway, dim light, bright/window glare, reflective surface, small concave play polygon, and a moving person crossing. Test foreground/background, incoming interruption, denied/revoked camera permission, offline transitions, device rotation, thermal pressure and moving away from the pet. Never infer obstacle protection from a screenshot of correct occlusion.

### Whole-experience acceptance run

Use a fresh release candidate and final content. Record app commit, build/config/catalog/profile versions, device/OS and test-room category.

1. Place the pet/home and walk around the composition; no camera-driver regression, floating home or unauthorized scale change.
2. Perform 30 consecutive valid player throws across short/medium distances, both input modes and several bearings. Require ≥95% completion without reset; classify every recovery. Invalid deliberate throws must always be rejected safely. No duplicate toy/completion is permitted in any run.
3. Move the player during return, leave/re-enter the play region, interrupt with Come/Sit/Home, and lose tracking during flight/pickup/return/drop. Every case terminates or resumes according to the spec with no stuck ownership.
4. Perform ten visible home entry/rest/exit cycles and at least one blocked-door scenario. No body/wall penetration beyond art tolerance, no concealed-renderer substitute, no trap after cancellation.
5. Disconnect after an entitled session begins; continue the same commands. Reconnect with duplicate queued events and confirm no duplicated durable effect. Test expiry/revocation separately with controlled clocks/fixtures.
6. Restart/reopen; identity/preferences/home selection return, room placement is requested again. Switch pets and verify fit/asset revalidation.
7. Complete ten-minute performance measurement plus twenty-minute thermal run. No crash, OS memory warning, native rendering error or unbounded memory growth; all inherited frame/memory budgets pass.

M6 adds blinded comparison of local-only versus supplemented behavior using at least 100 scenario seeds covering idle, player interruption, toy presence, home rest and rejected output. Reviewers rate coherence, variety and cue responsiveness with a fixed rubric. Ship only with a documented improvement and zero safety/authority regression. This is a planned evaluation, not a claim of statistical significance from an unrun test.

## 5. CI, packaging and release

Required pipeline order follows GW-ARCH-002: formatting/contract lint → assembly/provider boundary analysis → secret/dependency scan → service and Unity compile → EditMode → BuildGuard dry run → PlayMode → malicious/boundary asset fixtures → OpenAPI compatibility → migration invariants → Android/iOS smoke builds → recorded AR regression → physical-device release matrix.

The build manifest records source commit/dirty state, Unity/editor revision, lockfile hash, provider/graphics API, app ID/version, generated scene checksum, asset/profile/catalog hashes, signing key IDs (public identifiers only), feature defaults and gate result locations. An APK existing on disk is not release provenance.

Retain the known direct ARCore choice; re-enabling NSDK requires the ADR-012 upstream fix and device evidence. No package version change is hidden inside an animation, input or documentation PR. Any upgrade follows the repository's release-project rules.

Release stages: internal devices → limited invited pilot → 5% →25% →100%, with at least 24 h observation per public stage and explicit review of affected device cohorts. Small sample sizes require longer observation rather than an artificial success rate. Halt for asset integrity regression, crash-free sessions below 99.7%, repeated stuck fetch/home state, or failed memory/thermal gates. Optional model has its own flag and rollout.

## 6. Decisions and deadlines

| Decision | Selected working default | Owner / due |
|---|---|---|
| Spec conflicts | Adopt ADR-013's completion sequence; preserve existing product/trust limits | Lead + product, M0 review |
| Art staffing and source rights | Dedicated technical artist using existing hero derivatives; no runtime arbitrary model generation | Product/art lead, M0 |
| Minimum device matrix | Reference Pixel plus actual low-tier Android and two iOS capability classes | AR/QA, M0 |
| Home dimensions | Initial clear 0.70 ×0.90 m doorway and measured interior envelope | Technical art + gameplay, before M2 publishing |
| Identity/deployment providers | Existing architecture adapters; exact providers still undecided in ADR-001/002 | Backend/ops, before M4 staging |
| Constrained practice mode | Off until ADR-013 exception adopted and device validation complete | Lead/product, before M3 fallback release |
| Launch age/regions | Keep unapproved cohorts disabled; do not infer approval from database triggers | Product release owner, before external pilot |
| Optional model runtime/artifact | No vendor/model chosen without device/license benchmark | AI engineer, M6 spike |
| Arbitrary conversational/voice AI | Outside this release; require a separate interaction/privacy/model spec | Product, only if scope expands |

These decisions do not prevent implementing primitive interactions, final art, Core state machines or existing provider integration. They are tracked delivery dependencies, not a request to stop this architecture task.

## 7. Verification performed for this specification

- Source tree, selected Unity implementations, contract schemas, migrations, package pins, existing ADRs and the original PDF were read.
- The fresh checkout matched commit `73b36c758aaef98f23c206729fd31466a8cba190`.
- The new package is documentation plus a tuning example; runtime, production contracts, databases, assets and package pins are unchanged.
- Documentation links, JSON syntax, required tuning relationships, requirement-ID coverage and Git whitespace are checked during authoring.
- No new Unity test, Blender export, device session, server deployment or application release is claimed. Historical August 4 test counts remain attributed to the handoff.
